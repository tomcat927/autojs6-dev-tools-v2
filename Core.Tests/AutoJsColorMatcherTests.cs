using Core.Helpers;
using Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Core.Tests;

[TestClass]
public class AutoJsColorMatcherTests
{
    [TestMethod]
    public void ParseColor_ShouldSupportHashAndHexPrefix()
    {
        Assert.AreEqual(new RgbColor(0x12, 0x34, 0x56), AutoJsColorMatcher.ParseColor("#123456"));
        Assert.AreEqual(new RgbColor(0x12, 0x34, 0x56), AutoJsColorMatcher.ParseColor("0x123456"));
        Assert.AreEqual(new RgbColor(0x12, 0x34, 0x56), AutoJsColorMatcher.ParseColor("FF123456"));
    }

    [TestMethod]
    public void MatchDiff_ShouldUseRgbAbsoluteDifferenceSum()
    {
        var actual = new RgbColor(20, 30, 40);
        var expected = new RgbColor(18, 35, 44);

        var result = AutoJsColorMatcher.MatchDiff(actual, expected, threshold: 11);

        Assert.AreEqual(2, result.DeltaR);
        Assert.AreEqual(5, result.DeltaG);
        Assert.AreEqual(4, result.DeltaB);
        Assert.AreEqual(11, result.DeltaSum);
        Assert.IsTrue(result.IsMatch);
    }

    [TestMethod]
    public void MatchDiff_ShouldRejectWhenDifferenceExceedsThreshold()
    {
        var result = AutoJsColorMatcher.MatchDiff(
            new RgbColor(0, 0, 0),
            new RgbColor(10, 10, 10),
            threshold: 16);

        Assert.AreEqual(30, result.DeltaSum);
        Assert.IsFalse(result.IsMatch);
    }
}
