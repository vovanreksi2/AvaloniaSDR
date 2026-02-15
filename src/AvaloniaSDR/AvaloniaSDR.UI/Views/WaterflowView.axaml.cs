using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;

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
    private int[] pixelBuffer;
    private readonly WaterfallColorProvider colorProvider;
    private int currentRow = 0;

    private SignalDataPoint[] pendingPoints;
    private readonly object bufferLock = new object();

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

        bitmap = new WriteableBitmap(new PixelSize((int)bounds.Width, (int)bounds.Height), new Vector(96, 96));
        pixelBuffer = new int[(int)bounds.Width * (int)bounds.Height];
    }

    public override void Render(DrawingContext context)
    {
        if (bitmap == null) return;

        context.DrawImage(
            bitmap,
            new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height),
            new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height)
        );
    }

    private void UpdateWaterflow(SignalDataPoint[] points)
    {
        if (WaterflowPoints == null || WaterflowPoints.Length == 0) return;

        WriteRowTopDown(bitmap, points);
    }

    public void WriteRowTopDown(WriteableBitmap bitmap, SignalDataPoint[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        Span<uint> lineBuffer = stackalloc uint[width];
        for (int i = 0; i < width; i++)
            lineBuffer[i] = colorProvider.GetColor(points[i].SignalPower);

        using var fb = bitmap.Lock();
        
        unsafe
        {
            Span<uint> pixels = new((void*)fb.Address, width * height);
            pixels[..(width * (height - 1))].CopyTo(pixels[width..]);  

            lineBuffer.CopyTo(pixels); 
        }
    }
}
