# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

All commands run from the repo root. The solution is at `src/AvaloniaSDR/AvaloniaSDR.sln`.

```bash
dotnet restore src/AvaloniaSDR/AvaloniaSDR.sln
dotnet build src/AvaloniaSDR/AvaloniaSDR.sln
dotnet build src/AvaloniaSDR/AvaloniaSDR.sln --configuration Release
dotnet run --project src/AvaloniaSDR/AvaloniaSDR.UI/AvaloniaSDR.UI.csproj
dotnet test src/AvaloniaSDR/AvaloniaSDR.sln --configuration Release --verbosity normal
```

CI runs on Ubuntu with .NET 10.0 (`dotnet restore → build Release --no-restore → test --no-build`).

## Architecture

**Three projects:**
- `AvaloniaSDR.Constants` — `SDRConstants.cs` only; shared config values (frequency range, FFT points, signal params)
- `AvaloniaSDR.DataProvider` — signal generation and async streaming; no UI dependency
- `AvaloniaSDR.UI` — Avalonia MVVM application; depends on DataProvider

**Tech stack:** .NET 10, Avalonia 11.3, ReactiveUI, SkiaSharp (via Avalonia), MathNet.Numerics, Microsoft.Extensions.DI + Logging

### Data Flow

```
OneSignalDataGenerator  →  OneSignalDataProvider  →  MainWindowViewModel  →  Views
  (Gaussian signal           (PeriodicTimer 50ms,       (normalizes power       SpectrumView
   + noise, 1024pts)          bounded Channel<T>         0–1, updates             WaterflowView)
                              capacity:1 DropOldest)     SpectrumData +
                                                         FrameVersion)
```

`IDataProvider` exposes a `ChannelReader<SignalDataPoint[]>`. The VM reads it with `await foreach` and sets `SpectrumData` + increments `FrameVersion` to trigger renders.

### Rendering

Both views are custom `Control` subclasses with two styled properties: `*PointsProperty` (data) and `FrameVersionProperty` (triggers `InvalidateVisual()`).

- **SpectrumView** — renders via `StreamGeometry` (grid + spectrum line); geometries are rebuilt only when bounds or data change.
- **WaterflowView** — GPU-accelerated waterfall using a `SKBitmap` ring buffer. The UI thread enqueues frames into a bounded channel (capacity 64, DropOldest). The render thread processes them via a custom `SkiaDrawOperation`, writing pixels with unsafe pointer manipulation. Color mapping uses a pre-computed 1024-entry BGRA LUT (`WaterfallColorProvider`: blue→cyan→green→yellow→red).

### DI Setup

Configured in `App.axaml.cs → OnFrameworkInitializationCompleted()`:
- `IDataGenerator` → `OneSignalDataGenerator` (Singleton)
- `IDataProvider` → `OneSignalDataProvider` (Singleton)
- `MainWindowViewModel` is resolved from the container and set as the window's `DataContext`

### ViewLocator

Reflection-based `ViewModel → View` resolution by naming convention (`SomeViewModel` → `SomeView`). Implements `IDataTemplate`.

### Key Constants (`SDRConstants.cs`)

| Constant | Value |
|---|---|
| Frequency range | 90–110 MHz |
| FFT points | 1024 |
| Signal center | 100 MHz, width 0.5 MHz |
| Update interval | 50 ms (20 fps) |
| Noise base | −100 dBm ± 0.5 |

### Performance Notes

- Waterfall channel uses `DropOldest` to prevent UI thread blocking under load
- `volatile SKBitmap?` for lock-free visibility between UI and render threads
- Color LUT avoids per-pixel gradient calculation
- DataProvider channel capacity is 1 (always delivers latest frame to VM)
