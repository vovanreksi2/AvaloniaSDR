using AvaloniaSDR.DataProvider;
using AvaloniaSDR.DataProvider.Processing;
using NUnit.Framework;

namespace AvaloniaSDR.Tests.Processing;

[TestFixture]
public class MaxHoldDownsamplerTests
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
    public void Downsample_20_To_10_EachPixelHoldsMaxOfBucket()
    {
        // 20 input points → 10 output pixels. Each bucket is exactly 2 points.
        // Bucket 0: [10, 1], Bucket 1: [2, 20], Bucket 2: [30, 3], ...
        var input = MakePoints([10, 1, 2, 20, 30, 3, 4, 40, 50, 5, 6, 60, 70, 7, 8, 80, 90, 9, 10, 100]);
        var output = new double[10];

        _resampler.Resample(input, output);

        Assert.That(output, Is.EqualTo(new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }));
    }

    [Test]
    public void Downsample_20_To_7_UnevenBuckets_EachPixelHoldsMaxOfBucket()
    {
        // 20 input points → 7 output pixels. 20/7 ≈ 2.857, so bucket 0 gets 2 points,
        // buckets 1–6 get 3 points each. Peak position rotates: first, middle, last.
        // Bucket 0: [10,  1]        Bucket 1: [ 2, 20,  3]  Bucket 2: [ 4,  5, 30]
        // Bucket 3: [40,  6,  7]    Bucket 4: [ 8, 50,  9]  Bucket 5: [11, 12, 60]
        // Bucket 6: [70, 13, 14]
        var input = MakePoints([10, 1, 2, 20, 3, 4, 5, 30, 40, 6, 7, 8, 50, 9, 11, 12, 60, 70, 13, 14]);
        var output = new double[7];

        _resampler.Resample(input, output);

        Assert.That(output, Is.EqualTo(new double[] { 10, 20, 30, 40, 50, 60, 70 }));
    }

    [Test]
    public void Downsample_EachOutputPixel_IsMaxNotMean()
    {
        // 4 input points → 2 output pixels. Bucket 0: [10, 1], Bucket 1: [2, 20]
        var input = MakePoints([10.0, 1.0, 2.0, 20.0]);
        var output = new double[2];

        _resampler.Resample(input, output);

        Assert.That(output[0], Is.EqualTo(10.0));
        Assert.That(output[1], Is.EqualTo(20.0));
    }

    [Test]
    public void Downsample_SinglePointIn_SinglePointOut()
    {
        var input = MakePoints([42.0]);
        var output = new double[1];

        _resampler.Resample(input, output);

        Assert.That(output[0], Is.EqualTo(42.0));
    }

    [Test]
    public void Downsample_TwoPoints_To_One_ReturnsMax()
    {
        var input = MakePoints([-5.0, 3.0]);
        var output = new double[1];

        _resampler.Resample(input, output);

        Assert.That(output[0], Is.EqualTo(3.0));
    }

    [Test]
    public void Downsample_AllNegative_ReturnsLeastNegative()
    {
        var input = MakePoints([-100.0, -50.0, -80.0, -10.0]);
        var output = new double[2];

        _resampler.Resample(input, output);

        Assert.That(output[0], Is.EqualTo(-50.0));
        Assert.That(output[1], Is.EqualTo(-10.0));
    }
}
