using System;
using System.Collections.Generic;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.Net.Commands;

/// <summary>
/// Writes one entry per level so you can see which ones pass the <c>Logging:LogLevel</c> filter.
/// Edit appsettings.json while the app runs and click again - no restart needed.
/// </summary>
/// <remarks>
/// Uses the <c>LogXxx</c> extension methods on purpose, to show plain message templates next to the
/// source-generated <c>AppLog</c> methods. They're fine for rare messages; prefer [LoggerMessage] on hot paths.
/// </remarks>
public sealed class WriteSampleLogsCommand : DelegateBaseCommand
{
    public WriteSampleLogsCommand(ILogger<WriteSampleLogsCommand> logger)
        : base(logger)
    {
    }

    protected override void Invoke()
    {
        Logger.LogTrace("Sample trace: very detailed diagnostics, normally off");
        Logger.LogDebug("Sample debug: developer details, e.g. {CacheHits} cache hits", 42);
        Logger.LogInformation("Sample information: order {OrderId} submitted with {ItemCount} items", 12345, 3);
        Logger.LogWarning("Sample warning: disk space low on {Drive} ({FreePercent:P0} free)", "C:", 0.08);

        // A dictionary scope adds structured properties to every entry written inside it.
        using (Logger.BeginScope(new Dictionary<string, object> { ["BatchId"] = "B-7" }))
        {
            try
            {
                throw new InvalidOperationException("Sample exception for the log file");
            }
            catch (InvalidOperationException ex)
            {
                // Always pass the exception object itself; never just ex.Message.
                Logger.LogError(ex, "Sample error: processing batch failed");
            }
        }

        Logger.LogCritical("Sample critical: this would page someone");
    }
}
