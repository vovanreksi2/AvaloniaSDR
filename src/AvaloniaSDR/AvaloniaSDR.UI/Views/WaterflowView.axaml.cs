using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AvaloniaSDR.UI.Views;

public partial class WaterflowView : Control
{
    public static readonly StyledProperty<Point[]?> WaterflowPointsProperty =
    AvaloniaProperty.Register<WaterflowView, Point[]?>(nameof(WaterflowPoints));

    public Point[]? WaterflowPoints
    {
        get => GetValue(WaterflowPointsProperty);
        set => SetValue(WaterflowPointsProperty, value);
    } 

    private Size _lastSize;

    private bool oneFires = false;
    private WriteableBitmap? bitmap;
    private uint[] pixelBuffer;

    private Dictionary<double, Color> gradients;

    public WaterflowView()
    {
        InitializeComponent();

        var gradientStops = new List<(double offset, Color color)>
        {
            (0.0, (Color)Color.Parse("#0000ff")),
            (0.25, (Color)Color.Parse("#00ffff")),
            (0.5, (Color)Color.Parse("#00ff00")),
            (0.75, (Color)Color.Parse("#ffff00")),
            (1.0, Color.Parse("#ff0000")),
        };

        gradients = new Dictionary<double, Color>();
        int steps = 100; 
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps; 
            Color c = InterpolateColor(gradientStops, t);
            gradients[t] = c;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WaterflowPointsProperty)
        {
            var points = (Point[])change.NewValue!;
            UpdateWaterflow(points);
            InvalidateVisual();
        }

        if (change.Property != BoundsProperty) return;

        var bounds = (Rect)change.NewValue!;
        if (bounds.Width == 0 && bounds.Height == 0) return;
        if (_lastSize == bounds.Size) return;

        _lastSize = bounds.Size;

        bitmap = new WriteableBitmap(new PixelSize((int)bounds.Width, (int)bounds.Height), new Vector(96, 96));
        pixelBuffer = new uint[(int)bounds.Width*200];

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

    private void UpdateWaterflow(Point[] points)
    {
        if (WaterflowPoints == null || WaterflowPoints.Length == 0) return;

        MoveWaterflowBitmap(bitmap);

        WriteRow(bitmap, points);
    }

    private void MoveWaterflowBitmap(WriteableBitmap bitmap)
    {
        int width = bitmap.PixelSize.Width;
        int height = 200;

        int rowWidth = width;

        Array.Copy(
                pixelBuffer,
                0,
                pixelBuffer,
                rowWidth,
                (height - 1) * rowWidth
            );
    }

    public void WriteRow(WriteableBitmap bitmap, Point[] points)
    {
        int width = bitmap.PixelSize.Width;

        // Записуємо новий рядок у верхню частину
        for (int i = 0; i < width; i++)
        {
            var normalizedY = GetNormalizedY(points[i].Y);
            var color = GetColor(normalizedY);

            pixelBuffer[i] = color; // верхній рядок
        }

        byte[] byteBuffer = new byte[pixelBuffer.Length * 4];
        Buffer.BlockCopy(pixelBuffer, 0, byteBuffer, 0, byteBuffer.Length);

        using var fb = bitmap.Lock();
        Marshal.Copy(byteBuffer, 0, fb.Address, byteBuffer.Length);
    }
    private double GetNormalizedY(double signalPower)
    {
        var minDb = -120;
        var maxDb = -20;

        return (signalPower - minDb) / (maxDb - minDb);
    }

    private uint GetColor(double signalPower)
    {
        var signal = Math.Round(signalPower, 2);
      
        return gradients[signal].ToUInt32();
    }

    private Color InterpolateColor(List<(double offset, Color color)> stops, double t)
    {
        (double offset, Color color) lower = stops[0];
        (double offset, Color color) upper = stops[^1];

        foreach (var stop in stops)
        {
            if (stop.offset <= t)
                lower = stop;
            if (stop.offset >= t)
            {
                upper = stop;
                break;
            }
        }

        if (upper.offset == lower.offset)
            return lower.color;

        double factor = (t - lower.offset) / (upper.offset - lower.offset);

        byte r = (byte)(lower.color.R + factor * (upper.color.R - lower.color.R));
        byte g = (byte)(lower.color.G + factor * (upper.color.G - lower.color.G));
        byte b = (byte)(lower.color.B + factor * (upper.color.B - lower.color.B));

        return Color.FromRgb(r, g, b);
    }
}