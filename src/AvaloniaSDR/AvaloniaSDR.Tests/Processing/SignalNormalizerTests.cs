using AvaloniaSDR.Constants;
using AvaloniaSDR.DataProvider;
using AvaloniaSDR.UI.Processing.SignalNormalizer;
using NUnit.Framework;

namespace AvaloniaSDR.Tests.Processing;

[TestFixture]
public sealed class SignalNormalizerTests
{
    private ISignalNormalizer _normalizer = null!;

    [SetUp]
    public void SetUp() => _normalizer = new SignalNormalizer();
 

    [Test]
    public void Normalize_MinPower_MapsToZero()
    {
        var frame = MakeFrame(SDRConstants.SignalPowerStart);

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.EqualTo(0.0));
    }

    [Test]
    public void Normalize_MaxPower_MapsToOne()
    {
        var frame = MakeFrame(SDRConstants.SignalPowerMax);

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.EqualTo(1.0));
    }

    [Test]
    public void Normalize_MidpointPower_MapsToHalf()
    {
        var mid = (SDRConstants.SignalPowerStart + SDRConstants.SignalPowerMax) / 2.0;
        var frame = MakeFrame(mid);

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.EqualTo(0.5));
    }



    [Test]
    public void Normalize_AllPointsInFrame_AreNormalized()
    {
        var frame = new SignalDataPoint[]
        {
            new() { Frequency = 0, SignalPower = SDRConstants.SignalPowerStart },
            new() { Frequency = 1, SignalPower = SDRConstants.SignalPowerMax   },
            new() { Frequency = 2, SignalPower = (SDRConstants.SignalPowerStart + SDRConstants.SignalPowerMax) / 2.0 },
        };

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.EqualTo(0.0), "min");
        Assert.That(frame[1].SignalPower, Is.EqualTo(1.0), "max");
        Assert.That(frame[2].SignalPower, Is.EqualTo(0.5), "mid");
    }


    [Test]
    public void Normalize_MutatesOriginalArray()
    {
        var frame = MakeFrame(-70.0);
        var before = frame[0].SignalPower;

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.Not.EqualTo(before));
    }

    [Test]
    public void Normalize_EmptyFrame_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _normalizer.Normalize([]));
    }

    [Test]
    public void Normalize_BelowMinPower_ProducesNegativeValue()
    {
        var frame = MakeFrame(SDRConstants.SignalPowerStart - 10);

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.LessThan(0.0));
    }

    [Test]
    public void Normalize_AboveMaxPower_ProducesValueGreaterThanOne()
    {
        var frame = MakeFrame(SDRConstants.SignalPowerMax + 10);

        _normalizer.Normalize(frame);

        Assert.That(frame[0].SignalPower, Is.GreaterThan(1.0));
    }

    private static SignalDataPoint[] MakeFrame(double power) =>
        [new SignalDataPoint { Frequency = 100.0, SignalPower = power }];
}
