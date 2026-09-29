using System;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf;

/// <summary>
/// Log messages of the shared WPF plumbing. Event ids: 1000-1099 application/exception handling,
/// 2000-2099 commands. Apps use 3000+ for their own messages.
/// </summary>
internal static partial class WpfLog
{
    // ----- Application / global exception handling -----------------------------------------------

    [LoggerMessage(1003, LogLevel.Critical, "Startup failed, the application will close")]
    public static partial void StartupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(1005, LogLevel.Information, "Windows session ending ({Reason})")]
    public static partial void SessionEnding(this ILogger logger, string reason);

    [LoggerMessage(1010, LogLevel.Error, "Unhandled exception on the UI thread")]
    public static partial void UnhandledUiException(this ILogger logger, Exception exception);

    [LoggerMessage(1011, LogLevel.Error, "Unhandled {ExceptionType} reached the UI thread (details logged above)")]
    public static partial void UnhandledUiExceptionAlreadyLogged(this ILogger logger, string exceptionType);

    [LoggerMessage(1012, LogLevel.Critical, "Unhandled exception (terminating: {IsTerminating})")]
    public static partial void UnhandledDomainException(this ILogger logger, bool isTerminating, Exception? exception);

    [LoggerMessage(1013, LogLevel.Error, "Unobserved task exception")]
    public static partial void UnobservedTaskException(this ILogger logger, Exception exception);

    [LoggerMessage(1014, LogLevel.Critical, "{Count} unhandled UI exceptions within {WindowSeconds} s, shutting down to avoid an error loop")]
    public static partial void ErrorLoopDetected(this ILogger logger, int count, int windowSeconds);

    [LoggerMessage(1015, LogLevel.Error, "Could not show the error dialog")]
    public static partial void ErrorDialogFailed(this ILogger logger, Exception exception);

    // ----- Commands --------------------------------------------------------------------------------

    [LoggerMessage(2000, LogLevel.Debug, "{Command} executing")]
    public static partial void CommandExecuting(this ILogger logger, string command);

    [LoggerMessage(2001, LogLevel.Debug, "{Command} completed in {ElapsedMs} ms")]
    public static partial void CommandCompleted(this ILogger logger, string command, long elapsedMs);

    [LoggerMessage(2002, LogLevel.Debug, "{Command} ignored because it cannot execute right now")]
    public static partial void CommandSkipped(this ILogger logger, string command);

    [LoggerMessage(2003, LogLevel.Information, "{Command} canceled after {ElapsedMs} ms")]
    public static partial void CommandCanceled(this ILogger logger, string command, long elapsedMs);

    [LoggerMessage(2004, LogLevel.Warning, "{Command} failed after {ElapsedMs} ms, the user was informed")]
    public static partial void CommandFailedHandled(this ILogger logger, string command, long elapsedMs, Exception exception);

    [LoggerMessage(2005, LogLevel.Error, "{Command} failed after {ElapsedMs} ms")]
    public static partial void CommandFailed(this ILogger logger, string command, long elapsedMs, Exception exception);

    [LoggerMessage(2006, LogLevel.Error, "{Command} CanExecute threw, treating it as disabled")]
    public static partial void CanExecuteFailed(this ILogger logger, string command, Exception exception);
}
