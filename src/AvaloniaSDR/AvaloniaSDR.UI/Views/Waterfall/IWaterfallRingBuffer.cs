using System;
using Avalonia.Media.Imaging;

namespace AvaloniaSDR.UI.Views.Waterfall;

/// <summary>Manages a ring-buffer-backed <see cref="WriteableBitmap"/> for the waterfall display.</summary>
public interface IWaterfallRingBuffer : IDisposable
{
    WriteableBitmap? Bitmap { get; }
    int CurrentRow { get; }
    int FilledRows { get; }

    /// <summary>Allocates or reallocates the backing bitmap to the given pixel dimensions.</summary>
    void Resize(int pixelWidth, int pixelHeight, double scaling);

    /// <summary>Writes one row of already-resampled normalised values (each in [0,1]) into the ring buffer.</summary>
    void WriteRow(ReadOnlySpan<double> normalizedRow);
}
