using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Processing;
using NUnit.Framework;

namespace AvaloniaSDR.Tests.Processing;

file sealed class SpyResampler : ISpectrumResampler
{
    public int CallCount { get; private set; }
    public void Resample(ReadOnlySpan<SignalDataPoint> input, Span<double> output) => CallCount++;
}

[TestFixture]
public class AdaptiveResamplerTests
{
    private static SignalDataPoint[] MakePoints(int count, double power = 50.0)
    {
        var pts = new SignalDataPoint[count];
        for (int i = 0; i < count; i++)
            pts[i] = new SignalDataPoint { Frequency = i, SignalPower = power };
        return pts;
    }

    [Test]
    public void WhenInputLonger_CallsDownsampler()
    {
        var down = new SpyResampler();
        var up = new SpyResampler();
        var resampler = new AdaptiveSpectrumResampler(down, up);

        resampler.Resample(MakePoints(8), new double[4]);

        Assert.That(down.CallCount, Is.EqualTo(1));
        Assert.That(up.CallCount, Is.EqualTo(0));
    }

    [Test]
    public void WhenOutputLongerOrEqual_CallsUpsampler()
    {
        var down = new SpyResampler();
        var up = new SpyResampler();
        var resampler = new AdaptiveSpectrumResampler(down, up);
            
        resampler.Resample(MakePoints(4), new double[8]);

        Assert.That(up.CallCount, Is.EqualTo(1));
        Assert.That(down.CallCount, Is.EqualTo(0));
    }
}
