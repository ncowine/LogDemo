using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Observability;

/// <summary>
/// Central export for <see cref="LoggingHost"/>, configured by the <c>Telemetry</c> section:
/// <c>LoggingHost.Create(..., OpenTelemetryExport.FromConfiguration)</c>. With an empty endpoint it does
/// nothing except say so in the session header.
/// </summary>
public sealed class OpenTelemetryExport : ILoggingHostExtension
{
    private readonly TelemetryPipeline? pipeline;

    private OpenTelemetryExport(TelemetryOptions options, TelemetryPipeline? pipeline)
    {
        Options = options;
        this.pipeline = pipeline;
    }

    public TelemetryOptions Options { get; }

    public static OpenTelemetryExport FromConfiguration(LoggingHostContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        TelemetryOptions options = new TelemetryOptions();
        context.Configuration.GetSection(TelemetryOptions.SectionName).Bind(options);
        foreach (string problem in options.Normalize())
        {
            context.ReportConfigurationProblem(problem);
        }

        TelemetryPipeline? pipeline = options.IsEnabled
            ? TelemetryPipeline.CreateOtlp(
                options,
                TelemetryIdentity.ForCurrentUser(context.ApplicationName, context.ApplicationVersion, context.Session.SessionId))
            : null;

        return new OpenTelemetryExport(options, pipeline);
    }

    public void ConfigureLogging(ILoggingBuilder logging)
    {
        if (this.pipeline is not null)
        {
            logging.AddProvider(this.pipeline.LoggerProvider);
        }
    }

    public void WriteSessionHeader(ILogger logger)
    {
        if (Options.EndpointUri is not null)
        {
            logger.TelemetryEnabled(Options.EndpointUri.AbsoluteUri, Options.Environment);
        }
        else if (string.IsNullOrWhiteSpace(Options.Endpoint))
        {
            logger.TelemetryDisabled();
        }
        else
        {
            logger.TelemetryMisconfigured();
        }
    }

    /// <summary>Sends what is still queued, waiting at most a couple of seconds.</summary>
    public void Dispose() => this.pipeline?.Dispose();
}
