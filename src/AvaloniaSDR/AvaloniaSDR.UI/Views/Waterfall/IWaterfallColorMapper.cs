namespace AvaloniaSDR.UI.Views.Waterfall;

/// <summary>Maps a normalized signal power value (0.0–1.0) to a BGRA8888 color.</summary>
public interface IWaterfallColorMapper
{
    /// <param name="normalizedValue">Value in [0.0, 1.0]. Values outside this range are clamped.</param>
    /// <returns>Packed BGRA8888 color: 0xAARRGGBB.</returns>
    uint GetColor(double normalizedValue);
}
