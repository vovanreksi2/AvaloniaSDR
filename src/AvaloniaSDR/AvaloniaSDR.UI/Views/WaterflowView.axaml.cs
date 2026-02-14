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
    private readonly WaterfallColor colorProvider;

    public WaterflowView()
    {
        InitializeComponent();
        colorProvider = new WaterfallColor();
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
        pixelBuffer = new int[(int)bounds.Width * 200];
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
        //MoveWaterflowBitmap(bitmap);

        //WriteRow(bitmap, points);
    }

    //private void MoveWaterflowBitmap(WriteableBitmap bitmap)
    //{
    //    int width = bitmap.PixelSize.Width;
    //    int height = 200;

    //    int rowWidth = width;

    //    Array.Copy(
    //            pixelBuffer,
    //            0,
    //            pixelBuffer,
    //            rowWidth,
    //            (height - 1) * rowWidth
    //        );
    //}

    //public void WriteRow(WriteableBitmap bitmap, NormalizeSignalPoint[] points)
    //{
    //    int width = bitmap.PixelSize.Width;

    //    for (int i = 0; i < width; i++)
    //    {
    //        var color = colorProvider.GetColor(points[i].SignalPower);

    //        pixelBuffer[i] = color;
    //    }

    //    byte[] byteBuffer = new byte[pixelBuffer.Length * 4];
    //    Buffer.BlockCopy(pixelBuffer, 0, byteBuffer, 0, byteBuffer.Length);

    //    using var fb = bitmap.Lock();
    //    Marshal.Copy(byteBuffer, 0, fb.Address, byteBuffer.Length);
    //}

    private int currentRow = 0;

    public void WriteRowOptimized(WriteableBitmap bitmap, NormalizeSignalPoint[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = 200;

        // Записуємо кольори нового рядка у поточний рядок циклічного буфера
        for (int i = 0; i < width; i++)
        {
            // GetColor повертає uint ARGB → приводимо до int
            pixelBuffer[currentRow * width + i] = unchecked((int)colorProvider.GetColor(points[i].SignalPower));
        }

        // Лок бітмапу
        using var fb = bitmap.Lock();

        // Копіюємо всі рядки у WriteableBitmap циклічно
        for (int row = 0; row < height; row++)
        {
            int srcRow = (currentRow + row) % height;
            Marshal.Copy(
                pixelBuffer,
                srcRow * width,
                fb.Address + row * width * 4, // 4 байти на піксель
                width
            );
        }

        // Переходимо до наступного рядка
        currentRow = (currentRow + 1) % height;
    }

    public void WriteRowTopDown(WriteableBitmap bitmap, NormalizeSignalPoint[] points)
    {
        int width = bitmap.PixelSize.Width;
        int height = 200;

        // Записуємо новий рядок у циклічний буфер
        for (int i = 0; i < width; i++)
        {
            pixelBuffer[currentRow * width + i] = unchecked((int)colorProvider.GetColor(points[i].SignalPower));
        }

        using var fb = bitmap.Lock();


        // Відображаємо рядки у зворотному порядку
        for (int row = 0; row < height; row++)
        {
            // srcRow — індекс рядка у циклічному буфері
            int srcRow = (currentRow - row + height) % height; // <-- основна зміна

            // destRow — рядок у бітмапі
            int destRow = row;

            Marshal.Copy(
                pixelBuffer,
                srcRow * width,
                fb.Address + destRow * width * 4,
                width
            );
        }

        // Зміщуємо currentRow на наступний рядок
        currentRow = (currentRow + 1) % height;
    }
}