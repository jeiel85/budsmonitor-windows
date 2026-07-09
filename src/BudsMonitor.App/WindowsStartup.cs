using Microsoft.Win32;
using Serilog;

namespace BudsMonitor.App;

/// <summary>
/// Registers/unregisters the app for automatic start at Windows sign-in via the per-user
/// Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). No admin rights required;
/// the entry is scoped to the current user and points at the running executable.
/// </summary>
internal static class WindowsStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BudsMonitor";

    /// <summary>True if the Run entry currently exists.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Reading start-with-Windows state failed");
            return false;
        }
    }

    /// <summary>Adds or removes the Run entry so it matches <paramref name="enabled"/>.</summary>
    public static void Apply(bool enabled)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                // Quote the path so a space in the install directory doesn't split the command.
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Applying start-with-Windows (enabled={Enabled}) failed", enabled);
        }
    }
}
