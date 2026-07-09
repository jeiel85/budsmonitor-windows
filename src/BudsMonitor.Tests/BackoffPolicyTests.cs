using BudsMonitor.Application;

namespace BudsMonitor.Tests;

public sealed class BackoffPolicyTests
{
    private static BackoffPolicy Policy()
        => new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(2));

    [Fact]
    public void No_failures_yields_zero()
    {
        Assert.Equal(TimeSpan.Zero, Policy().DelayFor(0));
        Assert.Equal(TimeSpan.Zero, Policy().DelayFor(-3));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void Doubles_each_failure(int failures, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Policy().DelayFor(failures));
    }

    [Fact]
    public void Caps_at_max_delay()
    {
        var policy = Policy();
        // 2s * 2^9 = 1024s which exceeds the 120s cap.
        Assert.Equal(TimeSpan.FromMinutes(2), policy.DelayFor(10));
        Assert.Equal(TimeSpan.FromMinutes(2), policy.DelayFor(1000));
    }

    [Fact]
    public void Large_count_does_not_overflow()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), Policy().DelayFor(int.MaxValue));
    }

    [Fact]
    public void Invalid_configuration_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BackoffPolicy(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BackoffPolicy(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)));
    }
}
