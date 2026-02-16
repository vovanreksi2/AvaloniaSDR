using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Processing;
using AvaloniaSDR.UI.ViewModels;
using AvaloniaSDR.UI.Views.Waterfall;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AvaloniaSDR.UI.Views;

public partial class WaterflowView : Control
{
    private Size _lastSize;
    private double _scaling = 1.0;

    private WriteableBitmap? bitmap;
    private IWaterfallColorMapper? colorProvider;
    private ISpectrumResampler? resampler;
    private MainWindowViewModel? vm;
    private int currentRow = 0;
    private int _filledRows = 0;
    private double[] _resampleBuffer = Array.Empty<double>();
    private SignalDataPoint[] _lastFrame;
    private Compositor? _compositor;

    public WaterflowView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        vm = (VisualRoot as Window)?.DataContext as MainWindowViewModel;

        var sp = (Application.Current as App)!.ServiceProvider;
        colorProvider = sp.GetRequiredService<IWaterfallColorMapper>();
        resampler = sp.GetRequiredService<ISpectrumResampler>();

        _compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        _compositor?.RequestCompositionUpdate(OnCompositionTick);
    }

    private void OnCompositionTick()
    {
        var frame = vm?.GetLastFrame();
        if (frame != null)
        {
            if (frame != _lastFrame)
            {
                _lastFrame = frame;
            }
            UpdateWaterflow(frame);
        }


        InvalidateVisual();

        _compositor?.RequestCompositionUpdate(OnCompositionTick);
    }

    public override void Render(DrawingContext context)
    {
        DrawSpectrum(context);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != BoundsProperty) return;

        var bounds = (Rect)change.NewValue!;
        if (bounds.Width == 0 && bounds.Height == 0) return;
        if (_lastSize == bounds.Size) return;

        _lastSize = bounds.Size;
        
        RecreateBitmap(bounds.Size);
    }

    private void RecreateBitmap(Size size)
    {
        _scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        int pixelWidth = (int)(size.Width * _scaling);
        int pixelHeight = (int)(size.Height * _scaling);

        if (pixelWidth <= 0 || pixelHeight <= 0) return;

        bitmap?.Dispose();

        bitmap = new WriteableBitmap(
            new PixelSize(pixelWidth, pixelHeight),
            new Vector(96 * _scaling, 96 * _scaling),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        currentRow = 0;
        _filledRows = 0;
    }
    private void DrawSpectrum(DrawingContext context)
    {
        if (bitmap == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var viewWidth = Bounds.Width;
        var viewHeight = Bounds.Height;

        var pixelWidth = bitmap.PixelSize.Width;
        var pixelHeight = bitmap.PixelSize.Height;

        // currentRow = next write position (stale data) → excluded from display.
        // Newest row: currentRow+1, oldest displayed: currentRow-1.
        // Segment 1: rows currentRow+1..h-1 (newer) → top of screen
        // Segment 2: rows 0..effectiveSegment2Height-1 (older) → bottom of screen
        //context.FillRectangle(Brushes.Black, new Rect(0, 0, viewWidth, viewHeight));

        int newestRow = currentRow + 1;
        int segment1Height = pixelHeight - newestRow;

        // During fill-up, segment 2 contains unwritten (black) rows beyond what has
        // actually been populated. Cap it to only the rows that carry real data.
        int effectiveSegment2Height = _filledRows >= pixelHeight
            ? currentRow
            : Math.Max(0, _filledRows - segment1Height);

        // Exact 1:1 physical pixel mapping → no interpolation at the seam.
        // segment1Height physical px / scaling = logical px where segment1 ends exactly.
        var splitY = segment1Height / _scaling;

        if (segment1Height > 0)
        {
            var sourceRect1 = new Rect(0, newestRow, pixelWidth, segment1Height);
            var destRect1 = new Rect(0, 0, viewWidth, splitY);
            context.DrawImage(bitmap, sourceRect1, destRect1);
        }

        if (effectiveSegment2Height > 0)
        {
            var sourceRect2 = new Rect(0, 0, pixelWidth, effectiveSegment2Height);
            var destRect2 = new Rect(0, splitY, viewWidth, effectiveSegment2Height / _scaling);
            context.DrawImage(bitmap, sourceRect2, destRect2);
        }
    }

    private void UpdateWaterflow(SignalDataPoint[] points)
    {
        if (points == null || points.Length == 0) return;
        
        WriteRowTopDown(bitmap!, points);
    }

    public void WriteRowTopDown(WriteableBitmap bitmap, SignalDataPoint[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;

        var displayData = GetNormalizedData(points, width);

        Span<uint> lineBuffer = stackalloc uint[width];
        for (int i = 0; i < width; i++)
        {
            lineBuffer[i] = colorProvider!.GetColor(displayData[i]);
        }

        using var fb = bitmap.Lock();
        unsafe
        {
            uint* targetRow = (uint*)fb.Address + (currentRow * width);
            Span<uint> dest = new(targetRow, width);

            lineBuffer.CopyTo(dest);
        }
        currentRow = (currentRow - 1 + height) % height;
        if (_filledRows < height) _filledRows++;
    }

    
    private Span<double> GetNormalizedData(SignalDataPoint[] points, int width)
    {
        if (_resampleBuffer.Length != width)
        {
            _resampleBuffer = new double[width];
        }

        resampler!.Resample(points, _resampleBuffer);

        return _resampleBuffer.AsSpan();
    }
}
