using System;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Observability;

/// <summary>
/// Plugs the OpenTelemetry pipeline into the app's <see cref="ILoggerFactory"/> as one more provider.
/// </summary>
/// <remarks>
/// The pipeline lives in its own <c>OpenTelemetrySdk</c> so the host can flush and shut it down with a
/// deadline, which <c>ILoggingBuilder.AddOpenTelemetry()</c> does not allow. This provider forwards to
/// the SDK's logger factory. The alias makes the standard <c>Logging:OpenTelemetry:LogLevel</c> section
/// apply to it, hot reload included, exactly as it would for the SDK's own provider.
/// </remarks>
[ProviderAlias("OpenTelemetry")]
internal sealed class ExportLoggerProvider : ILoggerProvider
{
    private readonly ILoggerFactory target;

    public ExportLoggerProvider(ILoggerFactory target)
    {
        this.target = target;
    }

    public ILogger CreateLogger(string categoryName) => this.target.CreateLogger(categoryName);

    // The pipeline owns the target factory and disposes it.
    public void Dispose()
    {
    }
}
