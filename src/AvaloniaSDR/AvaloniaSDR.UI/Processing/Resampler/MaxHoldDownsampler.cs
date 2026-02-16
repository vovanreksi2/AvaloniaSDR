using AvaloniaSDR.DataProvider;
using System;

namespace AvaloniaSDR.UI.Processing.Resampler;

/// <summary>Downsamples by taking the maximum power value in each output pixel bucket.</summary>
public sealed class MaxHoldDownsampler : ISpectrumResampler
{
    public void Resample(ReadOnlySpan<SignalDataPoint> input, Span<double> output)
    {
        int targetWidth = output.Length;
        double pointsPerPixel = (double)input.Length / targetWidth;

        for (int i = 0; i < targetWidth; i++)
        {
            int start = (int)(i * pointsPerPixel);
            int end = (int)((i + 1) * pointsPerPixel);
            if (end > input.Length) end = input.Length;

            double max = double.MinValue;
            for (int j = start; j < end; j++)
            {
                if (input[j].SignalPower > max)
                    max = input[j].SignalPower;
            }
            output[i] = max;
        }
    }
}
