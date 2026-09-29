using System.Diagnostics;
using System.IO;

namespace LogDemo.App.NetFramework.Services;

public interface IShellLauncher
{
    /// <summary>Opens Explorer with <paramref name="filePath"/> selected (or its folder if the file is gone).</summary>
    void RevealFile(string filePath);
}

public sealed class ShellLauncher : IShellLauncher
{
    public void RevealFile(string filePath)
    {
        string arguments = File.Exists(filePath)
            ? $"/select,\"{filePath}\""
            : $"\"{Path.GetDirectoryName(filePath)}\"";

        // UseShellExecute defaults differ (true on .NET Framework, false on .NET), so be explicit.
        using (Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true }))
        {
        }
    }
}
