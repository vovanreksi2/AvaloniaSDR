using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using AvaloniaSDR.DataProvider;

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

    private CompositionCustomVisual? _customVisual;
    private WaterfallDataTexture? _dataTexture;
    private Size _lastSize;

    public WaterflowView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var compositionVisual = ElementComposition.GetElementVisual(this);
        if (compositionVisual == null) return;

        var compositor = compositionVisual.Compositor;
        _customVisual = compositor.CreateCustomVisual(new WaterfallVisualHandler());
        ElementComposition.SetElementChildVisual(this, _customVisual);

        UpdateVisualSize();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _dataTexture?.Dispose();
        _dataTexture = null;
        _customVisual = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FrameVersionProperty)
        {
            if (WaterflowPoints != null && _dataTexture != null)
            {
                UpdateWaterflow(WaterflowPoints);
            }
        }

        if (change.Property == BoundsProperty)
        {
            var bounds = (Rect)change.NewValue!;
            if (bounds.Width == 0 && bounds.Height == 0) return;
            if (_lastSize == bounds.Size) return;

            _lastSize = bounds.Size;
            RecreateDataTexture(bounds.Size);
            UpdateVisualSize();
        }
    }

    private void RecreateDataTexture(Size size)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

        int pixelWidth = (int)(size.Width * scaling);
        int pixelHeight = (int)(size.Height * scaling);

        if (pixelWidth <= 0 || pixelHeight <= 0) return;

        if (_dataTexture == null)
            _dataTexture = new WaterfallDataTexture(pixelWidth, pixelHeight);
        else
            _dataTexture.Resize(pixelWidth, pixelHeight);
    }

    private void UpdateVisualSize()
    {
        if (_customVisual == null) return;

        _customVisual.Size = new Vector(Bounds.Width, Bounds.Height);
    }

    private void UpdateWaterflow(SignalDataPoint[] points)
    {
        if (points.Length == 0 || _dataTexture == null || _customVisual == null) return;

        _dataTexture.WriteRow(points);

        var frameData = _dataTexture.CreateFrameData();
        _customVisual.SendHandlerMessage(frameData);
    }
}
