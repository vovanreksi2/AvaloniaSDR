using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvaloniaSDR.UI.Views.Waterfall;

internal sealed class WaterfallRingBuffer(IWaterfallColorMapper colorMapper) : IWaterfallRingBuffer
{
    private WriteableBitmap? _bitmap;
    private int _currentRow;
    private int _filledRows;

    public WriteableBitmap? Bitmap => _bitmap;
    public int CurrentRow => _currentRow;
    public int FilledRows => _filledRows;

    public void Resize(int pixelWidth, int pixelHeight, double scaling)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return;

        _bitmap?.Dispose();

        _bitmap = new WriteableBitmap(
            new PixelSize(pixelWidth, pixelHeight),
            new Vector(96 * scaling, 96 * scaling),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        _currentRow = 0;
        _filledRows = 0;
    }

    public void WriteRow(ReadOnlySpan<double> normalizedRow)
    {
        if (_bitmap == null) return;

        int width = _bitmap.PixelSize.Width;
        int height = _bitmap.PixelSize.Height;

        Span<uint> lineBuffer = stackalloc uint[width];
        for (int i = 0; i < width; i++)
        {
            lineBuffer[i] = colorMapper.GetColor(normalizedRow[i]);
        }

        using var fb = _bitmap.Lock();
        unsafe
        {
            uint* targetRow = (uint*)fb.Address + (_currentRow * width);
            Span<uint> dest = new(targetRow, width);
            lineBuffer.CopyTo(dest);
        }

        _currentRow = (_currentRow - 1 + height) % height;
        if (_filledRows < height) _filledRows++;
    }

    public void Dispose() => _bitmap?.Dispose();
}
