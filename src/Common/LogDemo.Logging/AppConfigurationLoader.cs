using System;
using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging;

/// <summary>
/// Builds configuration from <c>appsettings.json</c>, the per-user override file and environment variables,
/// so that a broken JSON file never crashes the app - neither at startup (before a logger exists) nor when
/// the file is edited while running (reload happens on a thread-pool thread, where an exception would
/// terminate the process). Problems are queued until <see cref="AttachLogger"/> is called, then logged.
/// </summary>
public sealed class AppConfigurationLoader
{
    private readonly ConcurrentQueue<FileLoadExceptionContext> pendingErrors = new ConcurrentQueue<FileLoadExceptionContext>();
    private ILogger? logger;

    /// <param name="environmentPrefix">e.g. <c>LOGDEMO_</c>, so <c>LOGDEMO_Logging__LogLevel__Default=Debug</c> works.</param>
    public IConfigurationRoot Build(AppPaths paths, string environmentPrefix)
    {
        if (paths is null)
        {
            throw new ArgumentNullException(nameof(paths));
        }

        try
        {
            Directory.CreateDirectory(paths.DataDirectory);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Only needed for the optional user settings file; carry on without it.
        }

        return Build(paths.AppSettingsFile, paths.UserSettingsFile, environmentPrefix);
    }

    /// <summary>Same as <see cref="Build(AppPaths, string)"/> with explicit file locations. Both files are optional.</summary>
    public IConfigurationRoot Build(string appSettingsFile, string userSettingsFile, string environmentPrefix)
    {
        return new ConfigurationBuilder()
            .SetFileLoadExceptionHandler(OnFileLoadError)
            .AddJsonFile(appSettingsFile, optional: true, reloadOnChange: true)
            .AddJsonFile(userSettingsFile, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(environmentPrefix)
            .Build();
    }

    /// <summary>Logs problems found so far and every later one (e.g. on reload).</summary>
    public void AttachLogger(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        while (this.pendingErrors.TryDequeue(out FileLoadExceptionContext? context))
        {
            logger.ConfigurationFileInvalid(context.Provider.Source.Path, context.Exception);
        }
    }

    private void OnFileLoadError(FileLoadExceptionContext context)
    {
        context.Ignore = true;
        ILogger? current = this.logger;
        if (current is null)
        {
            this.pendingErrors.Enqueue(context);
        }
        else
        {
            current.ConfigurationFileInvalid(context.Provider.Source.Path, context.Exception);
        }
    }
}
