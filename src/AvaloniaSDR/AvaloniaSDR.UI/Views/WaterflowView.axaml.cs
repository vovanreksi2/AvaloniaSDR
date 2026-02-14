using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaSDR.UI.ViewModels;
using System.Runtime.InteropServices;

namespace AvaloniaSDR.UI.Views;

public partial class WaterflowView : Control
{
    public static readonly StyledProperty<NormalizeSignalPoint[]?> WaterflowPointsProperty =
    AvaloniaProperty.Register<WaterflowView, NormalizeSignalPoint[]?>(nameof(WaterflowPoints));

    public NormalizeSignalPoint[]? WaterflowPoints
    {
        get => GetValue(WaterflowPointsProperty);
        set => SetValue(WaterflowPointsProperty, value);
    }

    private Size _lastSize;

    private WriteableBitmap? bitmap;
    private int[] pixelBuffer;
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

        if (change.Property == WaterflowPointsProperty)
        {
            var points = (NormalizeSignalPoint[])change.NewValue!;
            UpdateWaterflow(points);
            InvalidateVisual();
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
        base.Render(context);

        if (bitmap == null) return;

        context.DrawImage(
            bitmap,
            new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height),
            new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height)
        );
    }

    private void UpdateWaterflow(NormalizeSignalPoint[] points)
    {
        if (WaterflowPoints == null || WaterflowPoints.Length == 0) return;

        WriteRowTopDown(bitmap, points);
    }

    public void WriteRowTopDown(WriteableBitmap bitmap, NormalizeSignalPoint[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        for (int i = 0; i < width; i++)
        {
            pixelBuffer[currentRow * width + i] = unchecked((int)colorProvider.GetColor(points[i].SignalPower));
        }

        using var fb = bitmap.Lock();
        for (int row = 0; row < height; row++)
        {
            int srcRow = (currentRow - row + height) % height; 

            int destRow = row;

            Marshal.Copy(
                pixelBuffer,
                srcRow * width,
                fb.Address + destRow * width * 4,
                width
            );
        }

        currentRow = (currentRow + 1) % height;
    }
}