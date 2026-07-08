using BudsMonitor.Providers.Gatt;

namespace BudsMonitor.Tests;

public sealed class BatteryLevelParserTests
{
    [Theory]
    [InlineData(new byte[] { 0 }, true, 0)]
    [InlineData(new byte[] { 55 }, true, 55)]
    [InlineData(new byte[] { 100 }, true, 100)]
    [InlineData(new byte[] { 101 }, false, 0)]
    [InlineData(new byte[] { 255 }, false, 0)]
    [InlineData(new byte[] { }, false, 0)]
    [InlineData(new byte[] { 80, 1, 2 }, true, 80)]
    public void TryParse_ValidatesRange(byte[] value, bool expectedOk, int expectedPercent)
    {
        var ok = BatteryLevelParser.TryParse(value, out var percentage);

        Assert.Equal(expectedOk, ok);
        if (expectedOk)
        {
            Assert.Equal(expectedPercent, percentage);
        }
    }
}
