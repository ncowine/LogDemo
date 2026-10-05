using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Observability;

/// <summary>Source-generated log methods of the central export. Event ids 150-159.</summary>
internal static partial class ObservabilityLog
{
    [LoggerMessage(150, LogLevel.Information, "Central telemetry export to {Endpoint} (environment {Environment})")]
    public static partial void TelemetryEnabled(this ILogger logger, string endpoint, string environment);

    [LoggerMessage(151, LogLevel.Information, "Central telemetry export is off (Telemetry:Endpoint is empty), logging to the file only")]
    public static partial void TelemetryDisabled(this ILogger logger);

    [LoggerMessage(152, LogLevel.Warning, "Central telemetry export is off because the Telemetry configuration is invalid (see the warning above), logging to the file only")]
    public static partial void TelemetryMisconfigured(this ILogger logger);
}
