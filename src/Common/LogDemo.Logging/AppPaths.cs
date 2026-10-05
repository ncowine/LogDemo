using System;
using System.IO;
using System.Reflection;

namespace LogDemo.Logging;

/// <summary>
/// Per-user writable locations, derived from the entry assembly's Company/Product/Version attributes.
/// Never write next to the executable: under Program Files that fails for standard users.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(Assembly entryAssembly)
    {
        if (entryAssembly is null)
        {
            throw new ArgumentNullException(nameof(entryAssembly));
        }

        string company = entryAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Company";
        string product = entryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? entryAssembly.GetName().Name ?? "App";

        Version = GetVersion(entryAssembly);
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), company, product);
        DefaultLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), product, Version);
        UserSettingsFile = Path.Combine(DataDirectory, "appsettings.user.json");
        AppSettingsFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    /// <summary>The app version without build metadata, e.g. <c>1.2.0</c>. Safe to use as a folder name.</summary>
    public string Version { get; }

    /// <summary><c>%LOCALAPPDATA%\{Company}\{Product}</c></summary>
    public string DataDirectory { get; }

    /// <summary><c>Documents\{Product}\{Version}</c>, one folder per installed version.</summary>
    public string DefaultLogDirectory { get; }

    /// <summary>Installed defaults, next to the executable.</summary>
    public string AppSettingsFile { get; }

    /// <summary>Optional per-user overrides (e.g. support turns on Debug logging for one user).</summary>
    public string UserSettingsFile { get; }

    private static string GetVersion(Assembly entryAssembly)
    {
        string version = entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? entryAssembly.GetName().Version?.ToString()
            ?? "0.0.0";

        // Drop build metadata ("1.0.0+abc123"), otherwise every commit would get its own folder.
        int metadata = version.IndexOf('+');
        if (metadata >= 0)
        {
            version = version.Substring(0, metadata);
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            version = version.Replace(invalid, '_');
        }

        return version;
    }
}
