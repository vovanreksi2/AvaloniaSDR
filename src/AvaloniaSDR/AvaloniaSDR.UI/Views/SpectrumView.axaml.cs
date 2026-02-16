using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaSDR.DataProvider;

namespace AvaloniaSDR.UI.Views;

public partial class SpectrumView : Control
{
    public static readonly StyledProperty<SignalDataPoint[]?> SpectrumPointsProperty =
    AvaloniaProperty.Register<SpectrumView, SignalDataPoint[]?>(nameof(SpectrumPoints));

    public SignalDataPoint[]? SpectrumPoints
    {
        get => GetValue(SpectrumPointsProperty);
        set => SetValue(SpectrumPointsProperty, value);
    }

    public static readonly StyledProperty<long> FrameVersionProperty =
    AvaloniaProperty.Register<SpectrumView, long>(nameof(FrameVersion));

    public long FrameVersion
    {
        get => GetValue(FrameVersionProperty);
        set => SetValue(FrameVersionProperty, value);
    } 

    private Size _lastSize;

    private readonly Pen gridPen = new(Brushes.Gray, 1);
    private readonly Pen spectrumPen = new(Brushes.YellowGreen, 1);

    private StreamGeometry? gridGeometry;
    private PathGeometry? spectrumGeometry;
    private PathFigure? _spectrumFigure;
    private LineSegment[]? _spectrumSegments;

    public SpectrumView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SpectrumPointsProperty || change.Property == FrameVersionProperty)
        {
            UpdateSpectrumGeometry(_lastSize);
            InvalidateVisual();
        }

        if (change.Property != BoundsProperty) return;

        var bounds = (Rect)change.NewValue!;
        if (bounds.Width == 0 && bounds.Height == 0) return;

        if (_lastSize == bounds.Size) return;

        _lastSize = bounds.Size;
        
        CreateGridGeometry(bounds.Size);
        UpdateSpectrumGeometry(bounds.Size);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(Brushes.Black, Bounds);

        if (gridGeometry != null) 
            context.DrawGeometry(null, gridPen, gridGeometry);

        if (spectrumGeometry != null)
            context.DrawGeometry(null, spectrumPen, spectrumGeometry);
    }

    private void CreateGridGeometry(Size size)
    {
        var geometry = new StreamGeometry();

        using var ctx = geometry.Open();
        const int lineCount = 20;

        for (var i = 0; i <= lineCount; i++)
        {
            var x = i * size.Width / lineCount;
            var y = i * size.Height / lineCount;

            ctx.BeginFigure(new Point(0, y), false);
            ctx.LineTo(new Point(size.Width, y));

            ctx.BeginFigure(new Point(x, 0), false);
            ctx.LineTo(new Point(x, size.Height));
        }

        gridGeometry = geometry;
    }

    private void UpdateSpectrumGeometry(Size size)
    {
        if (SpectrumPoints == null || SpectrumPoints.Length == 0)
            return;

        var width = size.Width;
        var height = size.Height;
        var points = SpectrumPoints;

        if (_spectrumSegments == null || _spectrumSegments.Length != points.Length - 1)
        {
            _spectrumSegments = new LineSegment[points.Length - 1];
            for (var i = 0; i < _spectrumSegments.Length; i++)
                _spectrumSegments[i] = new LineSegment();

            var pathSegments = new PathSegments();
            pathSegments.AddRange(_spectrumSegments);

            _spectrumFigure = new PathFigure { IsClosed = false, Segments = pathSegments };
            spectrumGeometry = new PathGeometry { Figures = [_spectrumFigure] };
        }

        _spectrumFigure!.StartPoint = new Point(0, height - points[0].SignalPower * height);
        for (var i = 1; i < points.Length; i++)
        {
            _spectrumSegments[i - 1].Point = new Point(
                i * width / (points.Length - 1),
                height - points[i].SignalPower * height);
        }
    }


}
