using Avalonia.Media;
using System.Collections.Generic;

namespace AvaloniaSDR.UI.Views;

public partial class WaterflowView
{
    public class WaterfallColorProvider
    {
        private readonly uint[] lut = [];
        private const int lutSize = 1024;

        public WaterfallColorProvider()
        {
            var gradientStops = new List<(double offset, Color color)>
        {
            (0.0, Color.Parse("#0000ff")),
            (0.25, Color.Parse("#00ffff")),
            (0.5, Color.Parse("#00ff00")),
            (0.75, Color.Parse("#ffff00")),
            (1.0, Color.Parse("#ff0000")),
        };

            lut = BuildLut(gradientStops, lutSize);
        }

        private static uint[] BuildLut(List<(double offset, Color color)> stops, int size)
        {
            var lut = new uint[size];

            for (int i = 0; i < size; i++)
            {
                double value = (double)i / (size - 1);

                for (int s = 0; s < stops.Count - 1; s++)
                {
                    var (o1, c1) = stops[s];
                    var (o2, c2) = stops[s + 1];

                    if (value >= o1 && value <= o2)
                    {
                        double t = (value - o1) / (o2 - o1);

                        byte r = (byte)(c1.R + (c2.R - c1.R) * t);
                        byte g = (byte)(c1.G + (c2.G - c1.G) * t);
                        byte b = (byte)(c1.B + (c2.B - c1.B) * t);

                        lut[i] = (uint)(255 << 24 | r << 16 | g << 8 | b);
                        break;
                    }
                }
            }

            return lut;
        }

        public uint GetColor(double signalPower)
        {
            var index = (int)(signalPower * (lutSize - 1));

            return lut[index];

        }
    }
}