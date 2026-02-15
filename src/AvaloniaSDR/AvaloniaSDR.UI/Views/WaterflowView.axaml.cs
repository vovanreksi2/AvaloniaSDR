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

        double viewWidth = Bounds.Width;
        double viewHeight = Bounds.Height;

        int pixelWidth = bitmap.PixelSize.Width;
        int pixelHeight = bitmap.PixelSize.Height;

        double splitRatio = (double)(pixelHeight - currentRow) / pixelHeight;
        double splitY = Math.Round(viewHeight * splitRatio);

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

        WriteRowTopDown(bitmap, data);
    }

    public void WriteRowTopDown(WriteableBitmap bitmap, double[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        currentRow = (currentRow - 1 + height) % height;

        Span<uint> lineBuffer = stackalloc uint[width];
        for (int i = 0; i < width; i++)
        {
            lineBuffer[i] = colorProvider.GetColor(points[i]);
        }

        using var fb = bitmap.Lock();
        unsafe
        {
            uint* targetRow = (uint*)fb.Address + (currentRow * width);
            Span<uint> dest = new(targetRow, width);

            lineBuffer.CopyTo(dest);
        }
    }
}
