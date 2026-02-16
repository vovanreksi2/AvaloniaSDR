using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.UI.Processing.Resampler;
using AvaloniaSDR.UI.Views.Waterfall;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AvaloniaSDR.UI.Views;

public partial class WaterflowView : Control
{
    private Action _onCompositionTick;

    public static readonly StyledProperty<SignalDataPoint[]?> WaterfallPointsProperty =
        AvaloniaProperty.Register<WaterflowView, SignalDataPoint[]?>(nameof(WaterfallPoints));

    public SignalDataPoint[]? WaterfallPoints
    {
        get => GetValue(WaterfallPointsProperty);
        set => SetValue(WaterfallPointsProperty, value);
    }

    private Size _lastSize;
    private double _scaling = 1.0;

    private IWaterfallRingBuffer? _buffer;
    private ISpectrumResampler? _resampler;
    private SignalDataPoint[]? _lastFrame;
    private double[] _resampleBuffer = Array.Empty<double>();
    private Compositor? _compositor;

    public WaterflowView()
    {
        InitializeComponent();
        _onCompositionTick = OnCompositionTick;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var sp = (Application.Current as App)!.ServiceProvider;
        _buffer    = sp.GetRequiredService<IWaterfallRingBuffer>();
        _resampler = sp.GetRequiredService<ISpectrumResampler>();

        _compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        _compositor?.RequestCompositionUpdate(_onCompositionTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _buffer?.Dispose();
        _buffer = null;
    }

    private void OnCompositionTick()
    {
        if (WaterfallPoints is not null && WaterfallPoints != _lastFrame)
        {
            _lastFrame = WaterfallPoints;
            UpdateWaterflow(_lastFrame);
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

        var viewWidth   = Bounds.Width;
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
            context.DrawImage(bitmap,
                new Rect(0, newestRow, pixelWidth, segment1Height),
                new Rect(0, 0, viewWidth, splitY));
        }

        if (effectiveSegment2Height > 0)
        {
            context.DrawImage(bitmap,
                new Rect(0, 0, pixelWidth, effectiveSegment2Height),
                new Rect(0, splitY, viewWidth, effectiveSegment2Height / _scaling));
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
