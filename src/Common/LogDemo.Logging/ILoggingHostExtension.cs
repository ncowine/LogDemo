using System;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging;

/// <summary>
/// Optional add-on to <see cref="LoggingHost"/>, e.g. central export (<c>LogDemo.Logging.Observability</c>).
/// Lives in its own project so apps that only log to a file don't take on its dependencies.
/// </summary>
/// <remarks>
/// Disposed after the app's logger factory and before the log file is closed, so whatever the extension
/// still flushes at shutdown can be logged.
/// </remarks>
public interface ILoggingHostExtension : IDisposable
{
    /// <summary>Adds the extension's providers to the app's logger factory.</summary>
    void ConfigureLogging(ILoggingBuilder logging);

    /// <summary>Adds the extension's lines to the session header, see <see cref="LoggingHost.WriteSessionHeader"/>.</summary>
    void WriteSessionHeader(ILogger logger);
}
