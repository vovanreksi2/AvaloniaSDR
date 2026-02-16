using AvaloniaSDR.DataProvider;
using AvaloniaSDR.UI.Processing.Resampler;
using NUnit.Framework;

namespace AvaloniaSDR.Tests.Processing;

[TestFixture]
public class LinearUpsamplerTests
{
    private readonly AdaptiveSpectrumResampler _resampler = new(new MaxHoldDownsampler(), new LinearUpsamplingResampler());

    private static SignalDataPoint[] MakePoints(double[] powers)
    {
        var pts = new SignalDataPoint[powers.Length];
        for (int i = 0; i < powers.Length; i++)
            pts[i] = new SignalDataPoint { Frequency = i, SignalPower = powers[i] };
        return pts;
    }

    [Test]
    public void Upsample_10_To_100_InterpolatedValuesWithinSourceRange()
    {
        var input = MakePoints([0, 10, 20, 30, 40, 50, 60, 70, 80, 90]);
        double min = 0, max = 90;
        var output = new double[100];

        _resampler.Resample(input, output);

        foreach (var v in output)
            Assert.That(v, Is.InRange(min, max));
    }

    [Test]
    public void Upsample_EndpointsPreservedExactly()
    {
        var input = MakePoints([5.0, 15.0, 25.0, 35.0, 45.0]);
        var output = new double[50];

        _resampler.Resample(input, output);

        Assert.That(output[0], Is.EqualTo(5.0), "First output should equal first input");
        Assert.That(output[49], Is.EqualTo(45.0), "Last output should equal last input");
    }

    [Test]
    public void Upsample_MonotonicallyIncreasingInput_OutputIsMonotonic()
    {
        var input = MakePoints([10.0, 20.0, 30.0, 40.0, 50.0]);
        var output = new double[25];

        _resampler.Resample(input, output);

        for (int i = 1; i < output.Length; i++)
            Assert.That(output[i], Is.GreaterThanOrEqualTo(output[i - 1]),
                $"Output not monotonic at index {i}: {output[i - 1]} > {output[i]}");
    }

    [Test]
    public void Upsample_ConstantInput_AllOutputSameValue()
    {
        var input = MakePoints([7.0, 7.0, 7.0, 7.0]);
        var output = new double[20];

        _resampler.Resample(input, output);

        foreach (var v in output)
            Assert.That(v, Is.EqualTo(7.0));
    }

    [Test]
    public void Upsample_SinglePoint_AllOutputSameValue()
    {
        var input = MakePoints([99.0]);
        var output = new double[10];

        _resampler.Resample(input, output);

        foreach (var v in output)
            Assert.That(v, Is.EqualTo(99.0));
    }

    [Test]
    public void Upsample_CachedXCoords_ProducesSameResultOnSecondCall()
    {
        var input = MakePoints([0.0, 10.0, 20.0]);
        var output1 = new double[15];
        var output2 = new double[15];

        _resampler.Resample(input, output1);
        _resampler.Resample(input, output2);

        Assert.That(output2, Is.EqualTo(output1));
    }
}
