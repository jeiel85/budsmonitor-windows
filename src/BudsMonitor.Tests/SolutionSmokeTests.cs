using BudsMonitor.Application;
using BudsMonitor.Diagnostics;
using BudsMonitor.Domain;
using BudsMonitor.Infrastructure;
using BudsMonitor.Providers.AirPods;

namespace BudsMonitor.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void CoreProjectsExposeExpectedAssemblyMarkers()
    {
        Assert.Equal("BudsMonitor.Domain", DomainAssembly.Name);
        Assert.Equal("BudsMonitor.Application", ApplicationAssembly.Name);
        Assert.Equal("BudsMonitor.Infrastructure", InfrastructureAssembly.Name);
        Assert.Equal("BudsMonitor.Diagnostics", DiagnosticsAssembly.Name);
        Assert.Equal("BudsMonitor.Providers.AirPods", AirPodsProviderAssembly.Name);
    }
}
