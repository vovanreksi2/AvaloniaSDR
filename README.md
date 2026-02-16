# AvaloniaSDR

A software-defined radio (SDR) spectrum analyzer and waterfall display built with Avalonia UI and .NET 10.

![Platform](https://img.shields.io/badge/platform-.NET%2010-blue)
![UI](https://img.shields.io/badge/UI-Avalonia%2011.3-purple)

## Features

- Real-time spectrum display (20 fps) with a scrolling waterfall view
- Time-varying simulated signals over a configurable frequency range (90–110 MHz)
- Gaussian noise floor with configurable base level and random variance
- Adaptive spectrum resampling (max-hold downsampling, linear upsampling)
- BGRA8888 color LUT waterfall with blue→cyan→green→yellow→red gradient
- Reactive MVVM architecture using ReactiveUI

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build & Run

```bash
# Restore dependencies
dotnet restore src/AvaloniaSDR/AvaloniaSDR.sln

# Build (Debug)
dotnet build src/AvaloniaSDR/AvaloniaSDR.sln

# Build (Release)
dotnet build src/AvaloniaSDR/AvaloniaSDR.sln --configuration Release

# Run
dotnet run --project src/AvaloniaSDR/AvaloniaSDR.UI/AvaloniaSDR.UI.csproj

# Run tests
dotnet test src/AvaloniaSDR/AvaloniaSDR.sln --configuration Release --verbosity normal

# Run a single test class
dotnet test src/AvaloniaSDR/AvaloniaSDR.sln --filter "FullyQualifiedName~SignalNormalizerTests"
```

## Architecture

The solution contains four projects:

| Project | Description |
|---|---|
| `AvaloniaSDR.Constants` | Shared configuration constants (frequency range, FFT points, signal params) |
| `AvaloniaSDR.DataProvider` | Signal generation and async streaming; no UI dependency; uses MathNet.Numerics |
| `AvaloniaSDR.UI` | Avalonia MVVM application; depends on DataProvider |
| `AvaloniaSDR.Tests` | NUnit 4 unit tests |

### Data Flow

```
CompositeDataGenerator  →  SignalDataProvider  →  MainWindowViewModel  →  Views
  (NoiseDataGenerator        (PeriodicTimer 50ms,    (ISignalNormalizer        SpectrumView
   + N × SignalDataGenerator  bounded Channel<T>      maps power 0–1,          WaterfallView
   configured via fluent      capacity:1 DropOldest)  dispatches to UI           → ISpectrumResampler
   DataProviderBuilder)                               thread)                    → IWaterfallRingBuffer)
```

`IDataProvider` exposes a `ChannelReader<SignalDataPoint[]>`. The VM reads it with `await foreach`, normalizes each frame in-place via `ISignalNormalizer`, then dispatches `SpectrumData` to the UI thread.

### Signal Generation

Signals are configured in `App.axaml.cs` using a fluent builder:

```csharp
services.AddDataProvider(builder => builder
    .WithNoise()
    .AddSignal(new TimeVaryingSignalDescriptor(centerMHz, widthMHz, segments, Loop: true))
    ...);
```

- **`NoiseDataGenerator`** — Gaussian noise floor: `NoiseBaseLevel + Normal(0,1) * NoiseRandomLevel`
- **`SignalDataGenerator`** — Gaussian peak: `power * Exp(-Δf² / (2σ²))` with jitter; skips tail beyond 4σ
- **`TimeVaryingSignalDescriptor`** — Ordered `SignalSegment(Duration, Power)` pairs drive time-varying amplitude; optionally looping
- **`CompositeDataGenerator`** — Element-wise addition of all generators

### Signal Processing Pipeline

1. **`ISignalNormalizer`** (`SignalNormalizer`) — Normalizes `SignalPower` from `[−120, −20] dBm` → `[0.0, 1.0]` in-place
2. **`ISpectrumResampler`** (`AdaptiveSpectrumResampler`) — Resamples to view pixel width:
   - `MaxHoldDownsampler` — max-hold per bucket when downsampling
   - `LinearUpsamplingResampler` — linear spline when upsampling; caches computed X-coordinates

### Rendering

Both views are custom `Control` subclasses. Each has a `*PointsProperty` (data) and a separate `FrameVersionProperty` that triggers `InvalidateVisual()` on increment.

- **SpectrumView** — Renders grid lines and a spectrum line as `PathGeometry`; rebuilt only when bounds or data change
- **WaterfallView** — Ring-buffer `WriteableBitmap` with unsafe pointer arithmetic for pixel writes; uses a pre-computed 1024-entry BGRA8888 color LUT

### Key Constants

| Constant | Value |
|---|---|
| Frequency range | 90–110 MHz |
| FFT points | 1024 |
| Signal center | 100 MHz, width 0.1 MHz |
| Update interval | 50 ms (20 fps) |
| Noise base | −100 dBm, random ±2.5 |
| Power range | −120 to −20 dBm |
| Waterfall max rows | 200 |

## Tech Stack

- [.NET 10](https://dotnet.microsoft.com/)
- [Avalonia 11.3](https://avaloniaui.net/)
- [ReactiveUI](https://www.reactiveui.net/)
- [MathNet.Numerics](https://numerics.mathdotnet.com/)
- [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [NUnit 4](https://nunit.org/)
