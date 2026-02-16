using AvaloniaSDR.UI.Views.Waterfall;
using NUnit.Framework;

namespace AvaloniaSDR.Tests.Waterfall;

/// <summary>
/// Tests for <see cref="WaterfallColorMapper"/>.
///
/// Gradient stops:  0.00 → #0000FF (Blue)
///                  0.25 → #00FFFF (Cyan)
///                  0.50 → #00FF00 (Green)
///                  0.75 → #FFFF00 (Yellow)
///                  1.00 → #FF0000 (Red)
///
/// Packed format:   0xAARRGGBB  (alpha always 0xFF)
/// </summary>
[TestFixture]
public sealed class WaterfallColorMapperTests
{
    private IWaterfallColorMapper _mapper = null!;

    [SetUp]
    public void SetUp() => _mapper = new WaterfallColorMapper();

    // -----------------------------------------------------------------------
    // Exact gradient stop colors
    // -----------------------------------------------------------------------

    [Test]
    public void GetColor_AtZero_ReturnsBlue()
    {
        var color = _mapper.GetColor(0.0);

        Assert.That(R(color), Is.EqualTo(0),   "R");
        Assert.That(G(color), Is.EqualTo(0),   "G");
        Assert.That(B(color), Is.EqualTo(255), "B");
    }

    [Test]
    public void GetColor_AtQuarter_ReturnsCyan()
    {
        var color = _mapper.GetColor(0.25);

        // LUT has 1024 entries; 0.25 maps to index 255 (= int(0.25*1023)),
        // which is 255/1023 ≈ 0.2493 — just below the exact stop.
        // Interpolated value is ≈254 due to byte truncation. Allow ±1.
        Assert.That(R(color), Is.EqualTo(0),             "R");
        Assert.That(G(color), Is.EqualTo(255).Within(1), "G");
        Assert.That(B(color), Is.EqualTo(255),           "B");
    }

    [Test]
    public void GetColor_AtHalf_ReturnsGreen()
    {
        var color = _mapper.GetColor(0.5);

        Assert.That(R(color), Is.EqualTo(0),   "R");
        Assert.That(G(color), Is.EqualTo(255), "G");
        Assert.That(B(color), Is.EqualTo(0),   "B");
    }

    [Test]
    public void GetColor_AtThreeQuarters_ReturnsYellow()
    {
        var color = _mapper.GetColor(0.75);

        // Same LUT quantization as the 0.25 stop: index 766 maps to
        // 766/1023 ≈ 0.7488, just below 0.75. R interpolates to ≈254. Allow ±1.
        Assert.That(R(color), Is.EqualTo(255).Within(1), "R");
        Assert.That(G(color), Is.EqualTo(255),           "G");
        Assert.That(B(color), Is.EqualTo(0),             "B");
    }

    [Test]
    public void GetColor_AtOne_ReturnsRed()
    {
        var color = _mapper.GetColor(1.0);

        Assert.That(R(color), Is.EqualTo(255), "R");
        Assert.That(G(color), Is.EqualTo(0),   "G");
        Assert.That(B(color), Is.EqualTo(0),   "B");
    }

    // -----------------------------------------------------------------------
    // Alpha channel
    // -----------------------------------------------------------------------

    [TestCase(0.0)]
    [TestCase(0.25)]
    [TestCase(0.5)]
    [TestCase(0.75)]
    [TestCase(1.0)]
    public void GetColor_AlwaysFullyOpaque(double value)
    {
        var color = _mapper.GetColor(value);

        Assert.That(A(color), Is.EqualTo(255), $"alpha at value={value}");
    }

    // -----------------------------------------------------------------------
    // Clamping
    // -----------------------------------------------------------------------

    [Test]
    public void GetColor_BelowZero_ClampedToZero()
    {
        Assert.That(_mapper.GetColor(-0.1), Is.EqualTo(_mapper.GetColor(0.0)));
        Assert.That(_mapper.GetColor(-999), Is.EqualTo(_mapper.GetColor(0.0)));
    }

    [Test]
    public void GetColor_AboveOne_ClampedToOne()
    {
        Assert.That(_mapper.GetColor(1.1), Is.EqualTo(_mapper.GetColor(1.0)));
        Assert.That(_mapper.GetColor(999), Is.EqualTo(_mapper.GetColor(1.0)));
    }

    // -----------------------------------------------------------------------
    // Monotonic hue transitions (smoke test for LUT integrity)
    // -----------------------------------------------------------------------

    [Test]
    public void GetColor_BlueToCyan_BlueChannelDecreases()
    {
        // From 0.0 (blue, B=255) to 0.25 (cyan, B=255) blue stays high,
        // but green rises from 0 → 255. Verify midpoint has partial green.
        var mid = _mapper.GetColor(0.125);

        Assert.That(G(mid), Is.GreaterThan(0).And.LessThan(255),
            "green should be partially interpolated between blue and cyan");
        Assert.That(B(mid), Is.EqualTo(255), "blue stays saturated in this segment");
    }

    [Test]
    public void GetColor_CyanToGreen_BlueChannelDecreases()
    {
        // From 0.25 (cyan, B=255) to 0.50 (green, B=0): blue drops.
        var nearlyCyan  = _mapper.GetColor(0.26);
        var nearlyGreen = _mapper.GetColor(0.49);

        Assert.That(B(nearlyCyan), Is.GreaterThan(B(nearlyGreen)),
            "blue decreases from cyan toward green");
    }

    [Test]
    public void GetColor_GreenToYellow_RedChannelIncreases()
    {
        // From 0.50 (green, R=0) to 0.75 (yellow, R=255): red rises.
        var nearlyGreen  = _mapper.GetColor(0.51);
        var nearlyYellow = _mapper.GetColor(0.74);

        Assert.That(R(nearlyGreen), Is.LessThan(R(nearlyYellow)),
            "red increases from green toward yellow");
    }

    [Test]
    public void GetColor_YellowToRed_GreenChannelDecreases()
    {
        // From 0.75 (yellow, G=255) to 1.0 (red, G=0): green drops.
        var nearlyYellow = _mapper.GetColor(0.76);
        var nearlyRed    = _mapper.GetColor(0.99);

        Assert.That(G(nearlyYellow), Is.GreaterThan(G(nearlyRed)),
            "green decreases from yellow toward red");
    }

    // -----------------------------------------------------------------------
    // Helpers — unpack BGRA8888 packed uint (0xAARRGGBB)
    // -----------------------------------------------------------------------

    private static byte A(uint color) => (byte)((color >> 24) & 0xFF);
    private static byte R(uint color) => (byte)((color >> 16) & 0xFF);
    private static byte G(uint color) => (byte)((color >>  8) & 0xFF);
    private static byte B(uint color) => (byte)( color        & 0xFF);
}
