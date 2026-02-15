using AvaloniaSDR.DataProvider;
using System;
using System.Buffers;

namespace AvaloniaSDR.UI.Views;

/// <summary>
/// Manages a Gray8 (Alpha8) byte ring buffer for the waterfall display.
/// Each row stores normalized power values (0-255). The entire buffer
/// is uploaded as an SKImage for GPU shader consumption.
/// </summary>
public sealed class WaterfallDataTexture : IDisposable
{
    private byte[] _data;
    private int _width;
    private int _height;
    private int _currentRow;

    public int Width => _width;
    public int Height => _height;
    public float ScrollOffset => (float)((_currentRow - 1 + _height) % _height) / _height;

    public WaterfallDataTexture(int width, int height)
    {
        _width = width;
        _height = height;
        _data = new byte[width * height];
        _currentRow = 0;
    }

    /// <summary>
    /// Writes one row of normalized signal power data into the ring buffer.
    /// Data is resampled to match the texture width via downsample (max) or upsample (linear).
    /// </summary>
    public void WriteRow(SignalDataPoint[] points)
    {
        var rowSpan = _data.AsSpan(_currentRow * _width, _width);

        if (_width <= points.Length)
            DownsampleToRow(points, rowSpan);
        else
            UpsampleToRow(points, rowSpan);

        _currentRow = (_currentRow + 1) % _height;
    }

    private void DownsampleToRow(SignalDataPoint[] points, Span<byte> row)
    {
        double pointsPerPixel = (double)points.Length / _width;

        for (int i = 0; i < _width; i++)
        {
            int start = (int)(i * pointsPerPixel);
            int end = Math.Min((int)((i + 1) * pointsPerPixel), points.Length);

            double max = double.MinValue;
            for (int j = start; j < end; j++)
            {
                if (points[j].SignalPower > max)
                    max = points[j].SignalPower;
            }

            row[i] = (byte)(Math.Clamp(max, 0.0, 1.0) * 255.0);
        }
    }

    private void UpsampleToRow(SignalDataPoint[] points, Span<byte> row)
    {
        int srcLen = points.Length;
        for (int i = 0; i < _width; i++)
        {
            double srcPos = (double)i / (_width - 1) * (srcLen - 1);
            int lo = (int)srcPos;
            int hi = Math.Min(lo + 1, srcLen - 1);
            double frac = srcPos - lo;
            double value = points[lo].SignalPower * (1.0 - frac) + points[hi].SignalPower * frac;
            row[i] = (byte)(Math.Clamp(value, 0.0, 1.0) * 255.0);
        }
    }

    /// <summary>
    /// Creates a snapshot of the pixel data for thread-safe transfer to the render thread.
    /// Uses ArrayPool to avoid per-frame heap allocation.
    /// </summary>
    public WaterfallFrameData CreateFrameData()
    {
        int size = _width * _height;
        var snapshot = ArrayPool<byte>.Shared.Rent(size);
        Buffer.BlockCopy(_data, 0, snapshot, 0, size);

        return new WaterfallFrameData(snapshot, _width, _height, ScrollOffset, size);
    }

    public void Resize(int width, int height)
    {
        if (width == _width && height == _height) return;

        _width = width;
        _height = height;
        _data = new byte[width * height];
        _currentRow = 0;
    }

    public void Dispose()
    {
        _data = [];
    }
}

/// <summary>
/// Immutable frame data sent from UI thread to composition render thread.
/// </summary>
public readonly record struct WaterfallFrameData(
    byte[] PixelData,
    int Width,
    int Height,
    float ScrollOffset,
    int ActualSize)
{
    public void Return()
    {
        ArrayPool<byte>.Shared.Return(PixelData);
    }
}
