using System;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging;

/// <summary>
/// Source-generated log methods: the level check happens first and there is no boxing or
/// template parsing per call. Event ids 100-199 belong to the logging infrastructure.
/// </summary>
internal static partial class LoggingLog
{
    [LoggerMessage(100, LogLevel.Warning, "Logging configuration problem: {Problem}")]
    public static partial void ConfigurationProblem(this ILogger logger, string problem);

    [LoggerMessage(101, LogLevel.Warning, "Log directory {Requested} is not usable, falling back to {Fallback}")]
    public static partial void DirectoryFallback(this ILogger logger, string requested, string fallback, Exception exception);

    [LoggerMessage(102, LogLevel.Warning, "Logging extension could not be started, continuing with the log file only")]
    public static partial void ExtensionFailed(this ILogger logger, Exception exception);

    [LoggerMessage(110, LogLevel.Debug, "Deleted old log file {FileName} (last written {LastWriteUtc:u})")]
    public static partial void RetentionFileDeleted(this ILogger logger, string fileName, DateTime lastWriteUtc);

    [LoggerMessage(111, LogLevel.Warning, "Could not delete old log file {FileName}")]
    public static partial void RetentionFileDeleteFailed(this ILogger logger, string fileName, Exception exception);

    [LoggerMessage(112, LogLevel.Information, "Log retention in {Directory} (keep {RetentionDays} days): scanned {Scanned}, deleted {Deleted}, failed {Failed}")]
    public static partial void RetentionCompleted(this ILogger logger, string directory, int retentionDays, int scanned, int deleted, int failed);

    [LoggerMessage(113, LogLevel.Error, "Log retention cleanup failed")]
    public static partial void RetentionCrashed(this ILogger logger, Exception exception);

    [LoggerMessage(120, LogLevel.Information,
        "==== {Application} {Version} starting | {Framework} {Architecture} | {OperatingSystem} | pid {ProcessId} | session {SessionId}")]
    public static partial void SessionStarting(this ILogger logger, string application, string version, string framework,
        string architecture, string operatingSystem, int processId, string sessionId);

    [LoggerMessage(121, LogLevel.Information, "Log file {LogFile}, retention {RetentionDays} days")]
    public static partial void LogFileInfo(this ILogger logger, string logFile, int retentionDays);

    [LoggerMessage(122, LogLevel.Information, "==== {Application} exiting with code {ExitCode} after {UptimeSeconds:0.0} s")]
    public static partial void SessionEnding(this ILogger logger, string application, int exitCode, double uptimeSeconds);

    [LoggerMessage(130, LogLevel.Warning, "Configuration file {Path} could not be loaded, its settings are ignored")]
    public static partial void ConfigurationFileInvalid(this ILogger logger, string? path, Exception exception);
}
