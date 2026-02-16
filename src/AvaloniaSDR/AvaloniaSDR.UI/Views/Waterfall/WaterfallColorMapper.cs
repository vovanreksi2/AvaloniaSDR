using Avalonia.Media;
using System.Collections.Generic;

namespace AvaloniaSDR.UI.Views.Waterfall;

internal sealed class WaterfallColorMapper : IWaterfallColorMapper
{
    private const int LutSize = 1024;
    private readonly uint[] _lut;

    public WaterfallColorMapper()
    {
        var stops = new List<(double offset, Color color)>
        {
            (0.00, Color.Parse("#0000ff")),
            (0.25, Color.Parse("#00ffff")),
            (0.50, Color.Parse("#00ff00")),
            (0.75, Color.Parse("#ffff00")),
            (1.00, Color.Parse("#ff0000")),
        };

        _lut = BuildLut(stops, LutSize);
    }

    public uint GetColor(double normalizedValue)
    {
        var index = (int)(normalizedValue * (LutSize - 1));
        index = System.Math.Clamp(index, 0, LutSize - 1);
        return _lut[index];
    }

    private uint[] BuildLut(List<(double offset, Color color)> stops, int size)
    {
        var lut = new uint[size];

        for (int i = 0; i < size; i++)
        {
            double value = (double)i / (size - 1);

            for (int s = 0; s < stops.Count - 1; s++)
            {
                var (o1, c1) = stops[s];
                var (o2, c2) = stops[s + 1];

                if (value < o1 || value > o2) continue;

                double t = (value - o1) / (o2 - o1);

                byte r = (byte)(c1.R + (c2.R - c1.R) * t);
                byte g = (byte)(c1.G + (c2.G - c1.G) * t);
                byte b = (byte)(c1.B + (c2.B - c1.B) * t);

                // PixelFormat.Bgra8888: alpha=255, R, G, B
                lut[i] = (uint)(255 << 24 | r << 16 | g << 8 | b);
                break;
            }
        }

        return lut;
    }
}
