using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaSDR.UI.ViewModels;
using System.Linq;

namespace AvaloniaSDR.UI.Views;

public partial class SpectrumView : Control
{
    public static readonly StyledProperty<NormalizeSignalPoint[]?> SpectrumPointsProperty =
    AvaloniaProperty.Register<SpectrumView, NormalizeSignalPoint[]?>(nameof(SpectrumPoints));

    public NormalizeSignalPoint[]? SpectrumPoints
    {
        get => GetValue(SpectrumPointsProperty);
        set => SetValue(SpectrumPointsProperty, value);
    } 

    private Size _lastSize;

    private readonly Pen gridPen = new(Brushes.Gray, 1);
    private readonly Pen spectrumPen = new(Brushes.YellowGreen, 1);

    private StreamGeometry? gridGeometry;
    private StreamGeometry? spectrumGeometry;

    public SpectrumView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SpectrumPointsProperty)
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
        if (SpectrumPoints == null || !SpectrumPoints.Any())
            return;

        var width = size.Width;
        var height = size.Height;

        var points = SpectrumPoints;

        var geometry = new StreamGeometry();

        using var ctx = geometry.Open();

        for (var i = 0; i < points.Length; i++)
        {
            double x = i * width / (points.Length - 1);

            double y = height - points[i].SignalPower * height;

            if (i == 0)
                ctx.BeginFigure(new Point(x, y), false);
            else
                ctx.LineTo(new Point(x, y));
        }

        spectrumGeometry = geometry;
    }
}