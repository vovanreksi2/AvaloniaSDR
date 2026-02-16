using MathNet.Numerics.Interpolation;
using System.Buffers;

namespace AvaloniaSDR.DataProvider.Processing;

/// <summary>Upsamples using linear spline interpolation. X-coordinate arrays are cached per input size to avoid per-frame allocations.</summary>
public sealed class LinearUpsamplingResampler : ISpectrumResampler
{
    private double[]? _cachedX;
    private int _cachedInputLen;

    public void Resample(ReadOnlySpan<SignalDataPoint> input, Span<double> output)
    {
        int inputLen = input.Length;
        int targetLen = output.Length;

        if (inputLen == 0) { output.Clear(); return; }
        if (inputLen == 1) { output.Fill(input[0].SignalPower); return; }

        if (_cachedX == null || _cachedInputLen != inputLen)
        {
            _cachedX = new double[inputLen];
            for (int i = 0; i < inputLen; i++)
                _cachedX[i] = (double)i / (inputLen - 1);
            _cachedInputLen = inputLen;
        }

        double[] yOriginal = ArrayPool<double>.Shared.Rent(inputLen);
        try
        {
            for (int i = 0; i < inputLen; i++)
                yOriginal[i] = input[i].SignalPower;

            var interpolation = LinearSpline.InterpolateSorted(_cachedX, yOriginal[..inputLen]);

            for (int i = 0; i < targetLen; i++)
            {
                double xTarget = (double)i / (targetLen - 1);
                output[i] = interpolation.Interpolate(xTarget);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(yOriginal);
        }
    }
}
