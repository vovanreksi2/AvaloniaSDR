# AvaloniaSDR — Waterfall Pipeline Bottleneck Analysis

---

## Part 1 — Current Code Analysis (post-PR #12 refactor)

This section analyses the code as it exists after the `improvePerfomance` branch was
merged. The old `WriteRowTopDown` / `Marshal.Copy` architecture has been replaced by
a `SKBitmap` ring-buffer with `ICustomDrawOperation`. Three new bottlenecks were
introduced and two crash-risk bugs remain unaddressed.

---

### Bottleneck 1 — Double Row Write and Double `InvalidateVisual()` per Frame (High)

**Files:** [WaterflowView.axaml.cs](../AvaloniaSDR.UI/Views/WaterflowView.axaml.cs)
`OnPropertyChanged` (line 56), [MainWindow.axaml](../AvaloniaSDR.UI/Views/MainWindow.axaml) (line 41)

Both `WaterflowPoints` and `FrameVersion` are bound to the same `WaterflowView`
instance. The ViewModel sets them one after the other inside a single
`InvokeAsync` block:

```csharp
// MainWindowViewModel.cs
SpectrumData = frame;   // → fires WaterflowPointsProperty changed
FrameVersion++;         // → fires FrameVersionProperty changed
```

`OnPropertyChanged` reacts to **both** without discriminating:

```csharp
if (change.Property == WaterflowPointsProperty || change.Property == FrameVersionProperty)
{
    if (WaterflowPoints != null)
    {
        _rowChannel.Writer.TryWrite(WaterflowPoints);  // called TWICE per frame
        InvalidateVisual();                             // called TWICE per frame
    }
}
```

**Effect:** The same `SignalDataPoint[]` reference is enqueued twice per logical data
frame. `WriteRowIntoBitmap` writes two identical pixel rows instead of one, so the
waterfall scrolls at double speed and contains duplicate rows. Two `InvalidateVisual()`
calls schedule two render passes; under load these land in different vsyncs, doubling
GPU composite work.

**Fix:** Drive everything from a single property. Use `FrameVersionProperty` as the
sole trigger (it is always the last thing set), and remove the side-effect from
`WaterflowPointsProperty`'s branch in `OnPropertyChanged`.

---

### Bottleneck 2 — Bitmap Dispose Race Between UI Thread and Render Thread (High / Crash)

**File:** [WaterflowView.axaml.cs](../AvaloniaSDR.UI/Views/WaterflowView.axaml.cs)
`OnPropertyChanged` (line 60), `Render` (line 84)

When the control is resized the UI thread replaces and disposes the bitmap:

```csharp
var old = _skBitmap;
_skBitmap = new SKBitmap(...);   // new reference visible via volatile
_writeRow = 0;
old?.Dispose();                  // ← frees native pixel buffer immediately
```

The render thread (Avalonia compositor) snapshots the reference and passes it into
`SkiaDrawOperation`, which is called *after* `Render()` returns:

```csharp
var bitmap = _skBitmap;                              // local snapshot
while (_rowChannel.Reader.TryRead(out var row))
    WriteRowIntoBitmap(bitmap, row);                 // unsafe pointer write
context.Custom(new SkiaDrawOperation(bitmap, ...));  // bitmap captured for later
```

`volatile` guarantees the new reference is visible but does **not** synchronise
object lifetime. `old?.Dispose()` can free the native SKBitmap pixel buffer while
`SkiaDrawOperation.Render()` is still reading it on the compositor thread, producing
a use-after-free that crashes with an access violation or silently corrupts the GPU
surface.

**Fix:** Never dispose the old bitmap synchronously on the UI thread. Instead, post
it to the render thread for deferred disposal (e.g., queue it in a `ConcurrentQueue`
and drain on the next `Render()` call), or track whether any `SkiaDrawOperation` still
holds a reference before freeing.

---

### Bottleneck 3 — `GetColor()` Has No Bounds Check (High / Crash)

**File:** [WaterfallColorProvider.cs](../AvaloniaSDR.UI/Views/WaterfallColorProvider.cs)
`GetColor` (line 57)

```csharp
public uint GetColor(double signalPower)
{
    var index = (int)(signalPower * (lutSize - 1));
    return lut[index];   // no clamping — throws on out-of-range input
}
```

The LUT has 1024 entries (valid indices 0–1023). Normalized `signalPower` values
outside `[0.0, 1.0]` produce a negative index or an index > 1023.

The normalization in `MainWindowViewModel`:

```csharp
var tmp = SDRConstants.SignalPowerMax - SDRConstants.SignalPowerStart;   // = 100
frame[i].SignalPower = (frame[i].SignalPower - SDRConstants.SignalPowerStart) / tmp;
```

`OneSignalDataGenerator` adds Gaussian noise (`mean = -100 dBm, stddev = 0.5 dBm`).
Because the Gaussian distribution has infinite tails, raw values below `-120 dBm`
(→ index < 0) or above `-20 dBm` (→ index > 1023) are regularly produced, especially
near the signal peak where `SignalPower = 60 dBm` is added. Both cases throw
`IndexOutOfRangeException` on the render thread and crash the application.

**Fix:**

```csharp
var index = Math.Clamp((int)(signalPower * (lutSize - 1)), 0, lutSize - 1);
```

---

### Bottleneck 4 — `NotifyPixelsChanged()` Called Per Row Instead of Per Frame (Low)

**File:** [WaterflowView.axaml.cs](../AvaloniaSDR.UI/Views/WaterflowView.axaml.cs)
`WriteRowIntoBitmap` (line 112)

`bitmap.NotifyPixelsChanged()` is called after every row write inside `WriteRowIntoBitmap`.
`Render()` drains all pending rows before compositing:

```csharp
while (_rowChannel.Reader.TryRead(out var row))
    WriteRowIntoBitmap(bitmap, row);    // NotifyPixelsChanged inside — N times
```

When multiple frames accumulate in `_rowChannel` (after a UI stall or startup burst),
this is called N times per render pass. A single call after the drain loop is
sufficient.

**Fix:** Move `bitmap.NotifyPixelsChanged()` to `Render()`, after the `while` loop,
and remove it from `WriteRowIntoBitmap`.

---

### Bottleneck 5 — `bitmap.GetPixels()` Called Per Row (Low)

**File:** [WaterflowView.axaml.cs](../AvaloniaSDR.UI/Views/WaterflowView.axaml.cs)
`WriteRowIntoBitmap` (line 105)

```csharp
unsafe
{
    uint* pixels = (uint*)bitmap.GetPixels().ToPointer();  // P/Invoke per row
    uint* row = pixels + _writeRow * width;
    // ...
}
```

`bitmap.GetPixels()` is a P/Invoke call into Skia's C++ library. The base pixel
address is constant for the lifetime of the bitmap. Retrieving it once per drained
row (N calls per render pass when the channel has backlog) is unnecessary.

**Fix:** Hoist `GetPixels()` out of `WriteRowIntoBitmap` and pass `uint* basePixels`
as a parameter, or retrieve it once in `Render()` before the drain loop.

---

### Bottleneck 6 — Array Aliasing / In-Place Mutation (Medium / Latent)

**File:** [MainWindowViewModel.cs](../AvaloniaSDR.UI/ViewModels/MainWindowViewModel.cs)
`StartDataProviderAsync` (line 56)

```csharp
_ = Task.Run(async () =>
{
    await foreach (var frame in dataProvider.Reader.ReadAllAsync())
    {
        for (int i = 0; i < frame.Length; i++)
            frame[i].SignalPower = (...);   // mutates the source array in-place

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SpectrumData = frame;           // same reference stored in ViewModel
            FrameVersion++;
        });
    }
});
```

After `InvokeAsync`, `SpectrumData`, `WaterflowPoints`, and an entry in `_rowChannel`
all point to the same `SignalDataPoint[]`. The current implementation is safe only
because `GenerateData()` always allocates a fresh array. If the data provider is ever
changed to pool or reuse buffers (a natural next optimization), the background thread
would normalize a recycled array that the render thread is simultaneously reading in
`WriteRowIntoBitmap`, causing a data race.

**Fix:** Normalize into a pre-allocated copy rather than mutating the source array,
or document the contract that `GenerateData()` must always allocate.

---

### Summary of Current Bottlenecks

| # | Severity | Location | Issue |
|---|----------|----------|-------|
| 1 | **High** | `WaterflowView.OnPropertyChanged` | Double row enqueue + double `InvalidateVisual()` per frame |
| 2 | **High** | `WaterflowView.OnPropertyChanged` (resize) | Bitmap dispose race → use-after-free crash |
| 3 | **High** | `WaterfallColorProvider.GetColor` | No bounds clamp → `IndexOutOfRangeException` crash |
| 4 | **Low** | `WaterflowView.WriteRowIntoBitmap` | `NotifyPixelsChanged()` called N times per render pass |
| 5 | **Low** | `WaterflowView.WriteRowIntoBitmap` | `GetPixels()` P/Invoke called N times per render pass |
| 6 | **Medium** | `MainWindowViewModel.StartDataProviderAsync` | Latent data race if array pooling is ever introduced |

---

---

## Part 2 — Prior Analysis (pre-PR #12, `WriteRowTopDown` architecture)

The following is the analysis of the original `WriteableBitmap` / `Marshal.Copy`
implementation for historical reference. It was written before the `improvePerfomance`
branch was merged.

---

### Executive Summary (original)

The Waterfall view freezes were caused by **excessive per-frame CPU work** in
`WaterflowView.WriteRowTopDown`. Every incoming data frame triggered a full-bitmap
pixel copy (`O(width × height)`) instead of writing only the single new row
(`O(width)`). At 20 FPS with a 1024-wide bitmap, this translated to millions of
unnecessary `Marshal.Copy` operations per second, all on the UI thread. Combined with
per-frame array allocations and the absence of GPU offloading, the rendering pipeline
saturated the CPU and starved the UI message loop, resulting in visible freezes.

---

### Identified Bottlenecks (original)

| # | Severity | Component | Issue |
|---|----------|-----------|-------|
| B1 | **CRITICAL** | `WaterflowView.WriteRowTopDown` | Full bitmap copy every frame — O(W×H) instead of O(W) |
| B2 | **HIGH** | `WaterflowView.WriteRowTopDown` | N × `Marshal.Copy` calls per frame (one per row) |
| B3 | **HIGH** | `MainWindowViewModel` | New `SignalDataPoint[]` allocation every frame (GC pressure) |
| B4 | **HIGH** | `WaterflowView` | All pixel work on UI thread — blocks rendering |
| B5 | **MEDIUM** | `WaterflowView` | `bitmap.Lock()` / unlock overhead every frame |
| B6 | **MEDIUM** | `SpectrumView` | New `StreamGeometry` allocated every frame |
| B7 | **MEDIUM** | `MainWindowViewModel` | `SpectrumData` set from `Task.Run` — threading risk |
| B8 | **LOW** | `WaterfallColorProvider` | Per-pixel method call, no vectorization |
| B9 | **LOW** | Rendering | No GPU usage — entire pipeline is CPU-bound |

---

### What PR #12 Fixed vs What It Introduced

| Old Bottleneck | Status After PR #12 | Notes |
|---|---|---|
| B1 — Full O(W×H) bitmap copy | **Fixed** | Ring buffer + two-strip GPU composite replaces full copy |
| B2 — Per-row `Marshal.Copy` | **Fixed** | Replaced with unsafe pointer write (O(W) per row) |
| B3 — Per-frame array allocation | **Not fixed** | `GenerateData()` still allocates; safe but inefficient |
| B4 — Pixel work on UI thread | **Fixed** | Pixel writes moved to render thread in `Render()` |
| B5 — `bitmap.Lock()` per frame | **Fixed** | `SKBitmap` used directly; no lock needed |
| B6 — `StreamGeometry` per frame | **Not verified** | `SpectrumView` code unchanged |
| B7 — Cross-thread property set | **Fixed** | `Dispatcher.UIThread.InvokeAsync` now used |
| B8 — No LUT vectorization | **Unchanged** | LUT exists but loop is not vectorized |
| B9 — No GPU rendering | **Partially fixed** | Skia GPU composite via `ICustomDrawOperation` |
| — | **New: Bottleneck 1** | Double write/render per frame (both properties trigger) |
| — | **New: Bottleneck 2** | Bitmap dispose race on resize |
| — | **New: Bottleneck 3** | `GetColor()` no bounds check — crash |

---

### Original Architecture Detail — B1 Root Cause

For a 1024×600 bitmap at 20 FPS the old code performed:

- **Per frame:** 600 × `Marshal.Copy`, each copying 4 KB → **2.4 MB copied**
- **Per second:** 20 × 2.4 MB = **~48 MB/s** of pure memory copy, all on UI thread
- Only **1 row** (4 KB) of new data arrives per frame → **99.8% of work was redundant**

The new ring-buffer approach writes exactly 1 row per frame on the render thread and
uses a two-strip `DrawBitmap` composite on the GPU. This eliminates the B1 issue
entirely.

---

### Original Performance Projections (GPU approach)

| Metric | Old CPU approach | Current Skia ring buffer | Full GPU shader (future) |
|---|---|---|---|
| Data upload/frame | 2.4 MB | 4 KB (1 row) | 4 KB (1 row) |
| Color mapping | CPU loop | CPU loop | GPU parallel |
| Scrolling | CPU memmove 2.4 MB | GPU 2-strip blit | GPU UV offset (0 bytes) |
| CPU usage | ~15–30% | ~2–5% | < 1% |
| Frame time | 5–15 ms | < 1 ms | < 0.1 ms |
