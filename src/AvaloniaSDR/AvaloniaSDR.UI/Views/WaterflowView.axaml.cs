using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaSDR.DataProvider;
using MathNet.Numerics.Interpolation;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Linq;

namespace AvaloniaSDR.UI.Views;

public partial class WaterflowView : Control
{
    public static readonly StyledProperty<SignalDataPoint[]?> WaterflowPointsProperty =
    AvaloniaProperty.Register<WaterflowView, SignalDataPoint[]?>(nameof(WaterflowPoints));

    public SignalDataPoint[]? WaterflowPoints
    {
        get => GetValue(WaterflowPointsProperty);
        set => SetValue(WaterflowPointsProperty, value);
    }

    public static readonly StyledProperty<long> FrameVersionProperty =
    AvaloniaProperty.Register<WaterflowView, long>(nameof(FrameVersion));

    public long FrameVersion
    {
        get => GetValue(FrameVersionProperty);
        set => SetValue(FrameVersionProperty, value);
    }

    private Size _lastSize;

    private WriteableBitmap? bitmap;
    private readonly WaterfallColorProvider colorProvider;
    private int currentRow = 0;
    
    public WaterflowView()
    {
        InitializeComponent();
        colorProvider = new WaterfallColorProvider();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FrameVersionProperty)
        {
            if (WaterflowPoints != null)
            {
                UpdateWaterflow(WaterflowPoints);
                InvalidateVisual();
            }
        }

        if (change.Property != BoundsProperty) return;

        var bounds = (Rect)change.NewValue!;
        if (bounds.Width == 0 && bounds.Height == 0) return;
        if (_lastSize == bounds.Size) return;

        _lastSize = bounds.Size;
        
        RecreateBitmap(bounds.Size);
    }

    private void RecreateBitmap(Size size)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        int pixelWidth = (int)(size.Width * scaling);
        int pixelHeight = (int)(size.Height * scaling);

        if (pixelWidth <= 0 || pixelHeight <= 0) return;

        bitmap?.Dispose();

        bitmap = new WriteableBitmap(
            new PixelSize(pixelWidth, pixelHeight),
            new Vector(96 * scaling, 96 * scaling),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        currentRow = 0;
    }

    public override void Render(DrawingContext context)
    {
        if (bitmap == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var viewWidth = Bounds.Width;
        var viewHeight = Bounds.Height;

        var pixelWidth = bitmap.PixelSize.Width;
        var pixelHeight = bitmap.PixelSize.Height;

        var splitRatio = (double)(pixelHeight - currentRow) / pixelHeight;
        var splitY = Math.Round(viewHeight * splitRatio);

        var sourceRect1 = new Rect(0, currentRow, pixelWidth, pixelHeight - currentRow);
        var destRect1 = new Rect(0, 0, viewWidth, splitY);
        context.DrawImage(bitmap, sourceRect1, destRect1);

        if (currentRow > 0)
        {
            var sourceRect2 = new Rect(0, 0, pixelWidth, currentRow);
            var destRect2 = new Rect(0, splitY, viewWidth, viewHeight - splitY);
            context.DrawImage(bitmap, sourceRect2, destRect2);
        }
    }

    private void UpdateWaterflow(SignalDataPoint[] points)
    {
        if (WaterflowPoints == null || WaterflowPoints.Length == 0) return;
        
        var data = points.Select(p => p.SignalPower).ToArray();

        WriteRowTopDown(bitmap!, data);
    }

    public void WriteRowTopDown(WriteableBitmap bitmap, double[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        var displayData = GetNormalizedData(points, width);

        Span<uint> lineBuffer = stackalloc uint[width];
        for (int i = 0; i < width; i++)
        {
            lineBuffer[i] = colorProvider.GetColor(displayData[i]);
        }

        using var fb = bitmap.Lock();
        unsafe
        {
            uint* targetRow = (uint*)fb.Address + (currentRow * width);
            Span<uint> dest = new(targetRow, width);

            lineBuffer.CopyTo(dest);
        }
        currentRow = (currentRow - 1 + height) % height;

    }

    private double[] GetNormalizedData(double[] points, int width) => width < points.Length
            ? DownsampleWithMax(points, width)
            : UpsampleSpectrum(points, width);

    public double[] DownsampleWithMax(double[] rawPowerData, int targetWidth)
    {
        if (targetWidth >= rawPowerData.Length)
            return rawPowerData;  

        var vector = Vector<double>.Build.Dense(rawPowerData);
        var result = new double[targetWidth];

        var pointsPerPixel = (double)rawPowerData.Length / targetWidth;

        for (var i = 0; i < targetWidth; i++)
        {
            var start = (int)(i * pointsPerPixel);
            var end = (int)((i + 1) * pointsPerPixel);

            if (end > rawPowerData.Length) end = rawPowerData.Length;

            var segment = vector.SubVector(start, end - start);
            result[i] = segment.Maximum();
        }

        return result;
    }

    public double[] UpsampleSpectrum(double[] smallArray, int targetSize)
    {
        double[] xOriginal = new double[smallArray.Length];
        for (int i = 0; i < smallArray.Length; i++)
            xOriginal[i] = (double)i / (smallArray.Length - 1);

        var interpolation = LinearSpline.InterpolateSorted(xOriginal, smallArray);

        double[] upsampled = new double[targetSize];
        for (int i = 0; i < targetSize; i++)
        {
            double xTarget = (double)i / (targetSize - 1);

            upsampled[i] = interpolation.Interpolate(xTarget);
        }

        return upsampled;
    }
}
