using System.Globalization;
using System.Runtime.InteropServices;

namespace BudsMonitor.Diagnostics;

/// <summary>
/// Collects a snapshot of the runtime environment for the diagnostics bundle.
/// Machine and user names are deliberately omitted so the bundle carries no personal
/// identifiers even if the user shares it.
/// </summary>
public static class DiagnosticsEnvironment
{
    public static IReadOnlyDictionary<string, string?> Collect(string? appVersion, DateTimeOffset generatedAt)
    {
        return new Dictionary<string, string?>
        {
            ["generatedAt"] = generatedAt.ToString("o", CultureInfo.InvariantCulture),
            ["appVersion"] = appVersion,
            ["osDescription"] = RuntimeInformation.OSDescription,
            ["osVersion"] = Environment.OSVersion.VersionString,
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["frameworkDescription"] = RuntimeInformation.FrameworkDescription,
            ["clrVersion"] = Environment.Version.ToString(),
            ["is64BitProcess"] = Environment.Is64BitProcess.ToString(),
            ["processorCount"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            ["currentCulture"] = CultureInfo.CurrentCulture.Name,
            ["currentUICulture"] = CultureInfo.CurrentUICulture.Name,
            ["timeZone"] = TimeZoneInfo.Local.Id,
        };
    }
}
