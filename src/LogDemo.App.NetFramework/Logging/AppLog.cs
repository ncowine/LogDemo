using System;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.NetFramework.Logging;

/// <summary>
/// This app's log messages in one place: stable event ids, consistent wording, source-generated
/// for zero allocations when a level is disabled. The shared libraries use ids below 3000.
/// </summary>
/// <remarks>
/// Event id ranges: 3000 customers, 3100 navigation, 4000 diagnostics.
/// Never log personal data (names, e-mail addresses): log ids and let support look the rest up.
/// </remarks>
internal static partial class AppLog
{
    // ----- Customers ---------------------------------------------------------------------------

    [LoggerMessage(3000, LogLevel.Information, "Loaded {Count} customers")]
    public static partial void CustomersLoaded(this ILogger logger, int count);

    [LoggerMessage(3001, LogLevel.Information, "Removed customer {CustomerId}")]
    public static partial void CustomerRemoved(this ILogger logger, int customerId);

    [LoggerMessage(3002, LogLevel.Debug, "User declined removing customer {CustomerId}")]
    public static partial void CustomerRemovalDeclined(this ILogger logger, int customerId);

    [LoggerMessage(3010, LogLevel.Debug, "Backend call {Operation} started")]
    public static partial void BackendCallStarted(this ILogger logger, string operation);

    [LoggerMessage(3011, LogLevel.Debug, "Backend call {Operation} returned in {ElapsedMs} ms")]
    public static partial void BackendCallCompleted(this ILogger logger, string operation, long elapsedMs);

    [LoggerMessage(3012, LogLevel.Warning, "Backend call {Operation} failed (simulated)")]
    public static partial void BackendCallFailedSimulated(this ILogger logger, string operation);

    // ----- Navigation --------------------------------------------------------------------------

    [LoggerMessage(3100, LogLevel.Information, "Navigated to {Target}")]
    public static partial void NavigationSucceeded(this ILogger logger, string target);

    [LoggerMessage(3101, LogLevel.Error, "Navigation to {Target} failed")]
    public static partial void NavigationFailed(this ILogger logger, string target, Exception? exception);

    [LoggerMessage(3102, LogLevel.Debug, "{ViewModel} activated")]
    public static partial void ViewActivated(this ILogger logger, string viewModel);

    [LoggerMessage(3103, LogLevel.Debug, "{ViewModel} deactivated")]
    public static partial void ViewDeactivated(this ILogger logger, string viewModel);

    [LoggerMessage(3110, LogLevel.Information, "Startup completed in {ElapsedMs} ms")]
    public static partial void StartupCompleted(this ILogger logger, long elapsedMs);

    // ----- Diagnostics -------------------------------------------------------------------------

    [LoggerMessage(4000, LogLevel.Information, "Opened log folder {Directory}")]
    public static partial void LogFolderOpened(this ILogger logger, string directory);

    [LoggerMessage(4001, LogLevel.Information, "Simulated backend failures {State}")]
    public static partial void SimulatedFailuresToggled(this ILogger logger, string state);

    [LoggerMessage(4002, LogLevel.Warning, "Simulating an unhandled exception on the {Target}")]
    public static partial void SimulatingCrash(this ILogger logger, string target);
}
