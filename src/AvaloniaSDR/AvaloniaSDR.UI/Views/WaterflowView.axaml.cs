using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    private IWaterfallRingBuffer? _buffer;
    private ISpectrumResampler? _resampler;
    private SignalDataPoint[]? _lastFrame;
    private MainWindowViewModel? vm;
    private double[] _resampleBuffer = Array.Empty<double>();
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
        _buffer   = sp.GetRequiredService<IWaterfallRingBuffer>();
        _resampler = sp.GetRequiredService<ISpectrumResampler>();

        _compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        _compositor?.RequestCompositionUpdate(OnCompositionTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _buffer?.Dispose();
        _buffer = null;
    }

    private void OnCompositionTick()
    {
        var frame = vm?.GetLastFrame();
        if (frame is { Length: > 0 } && frame != _lastFrame)
        {
            _lastFrame = frame;
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

        _scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        int pixelWidth  = (int)(_lastSize.Width  * _scaling);
        int pixelHeight = (int)(_lastSize.Height * _scaling);
        _buffer?.Resize(pixelWidth, pixelHeight, _scaling);
    }

    private void DrawSpectrum(DrawingContext context)
    {
        var bitmap = _buffer?.Bitmap;
        if (bitmap == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var viewWidth  = Bounds.Width;
        var viewHeight = Bounds.Height;
        var pixelWidth  = bitmap.PixelSize.Width;
        var pixelHeight = bitmap.PixelSize.Height;

        int currentRow  = _buffer!.CurrentRow;
        int filledRows  = _buffer!.FilledRows;

        int newestRow      = currentRow + 1;
        int segment1Height = pixelHeight - newestRow;

        int effectiveSegment2Height = filledRows >= pixelHeight
            ? currentRow
            : Math.Max(0, filledRows - segment1Height);

        var splitY = segment1Height / _scaling;

        if (segment1Height > 0)
        {
            var sourceRect1 = new Rect(0, newestRow, pixelWidth, segment1Height);
            var destRect1   = new Rect(0, 0, viewWidth, splitY);
            context.DrawImage(bitmap, sourceRect1, destRect1);
        }

        if (effectiveSegment2Height > 0)
        {
            var sourceRect2 = new Rect(0, 0, pixelWidth, effectiveSegment2Height);
            var destRect2   = new Rect(0, splitY, viewWidth, effectiveSegment2Height / _scaling);
            context.DrawImage(bitmap, sourceRect2, destRect2);
        }
    }

    private void UpdateWaterflow(SignalDataPoint[] points)
    {
        if (_buffer?.Bitmap == null) return;

        int width = _buffer.Bitmap.PixelSize.Width;
        if (_resampleBuffer.Length != width)
            _resampleBuffer = new double[width];

        _resampler!.Resample(points, _resampleBuffer);
        _buffer.WriteRow(_resampleBuffer);
    }
}
