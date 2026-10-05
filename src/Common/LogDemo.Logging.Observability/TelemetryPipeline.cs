using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LogDemo.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace LogDemo.Logging.Observability;

/// <summary>
/// Logs and traces to the OpenTelemetry Collector over OTLP/HTTP. Best effort by design: batched in the
/// background, never blocks the UI thread, and gives up after a short deadline at shutdown. The local file
/// stays the complete record; this is how problems get noticed without anyone reporting them.
/// </summary>
internal sealed class TelemetryPipeline : IDisposable
{
    // At shutdown (normal exit or a crash) the app waits at most this long per signal for the last batch.
    private const int LogShutdownTimeoutMs = 2000;
    private const int TraceShutdownTimeoutMs = 1000;

    // An unreachable collector must fail an export quickly instead of holding a batch for the default 10 s.
    private const int ExportTimeoutMs = 5000;
    private const int ScheduledDelayMs = 2000;
    private const int MaxQueueSize = 2048;
    private const int MaxExportBatchSize = 512;

    private readonly OpenTelemetrySdk sdk;
    private readonly ILoggerFactory sdkLoggerFactory;

    internal TelemetryPipeline(
        TelemetryIdentity identity,
        string environment,
        Action<LoggerProviderBuilder> addLogExport,
        Action<TracerProviderBuilder> addTraceExport)
    {
        this.sdk = OpenTelemetrySdk.Create(builder =>
        {
            // The SDK's own logger factory would drop Debug. Level filtering happens in the app's factory
            // (Logging:OpenTelemetry:LogLevel), so everything that reaches this one is exported.
            builder.Services.AddLogging(logging => logging.AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.Trace));
            builder
                .ConfigureResource(resource => ConfigureResource(resource, identity, environment))
                .WithLogging(
                    logging =>
                    {
                        logging.AddProcessor(new SessionLogProcessor(identity.UserName, identity.SessionId));
                        addLogExport(logging);
                    },
                    options =>
                    {
                        // Rendered message as the log body, scopes (command name + id) as attributes.
                        options.IncludeFormattedMessage = true;
                        options.IncludeScopes = true;
                    })
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(AppTracing.SourceName)
                        .AddHttpClientInstrumentation()
                        .AddProcessor(new SessionSpanProcessor(identity.UserName, identity.SessionId));
                    addTraceExport(tracing);
                });
        });

        this.sdkLoggerFactory = this.sdk.GetLoggerFactory();
        LoggerProvider = new ExportLoggerProvider(this.sdkLoggerFactory);
    }

    /// <summary>Add to the app's logger factory; filtered by <c>Logging:OpenTelemetry:LogLevel</c>.</summary>
    public ILoggerProvider LoggerProvider { get; }

    public static TelemetryPipeline CreateOtlp(TelemetryOptions options, TelemetryIdentity identity)
    {
        Uri endpoint = options.EndpointUri ?? throw new ArgumentException("Telemetry export is not enabled.", nameof(options));
        string? headers = options.Headers;

        return new TelemetryPipeline(
            identity,
            options.Environment,
            logging => logging.AddProcessor(new BatchLogRecordExportProcessor(
                new OtlpLogExporter(ExporterOptions(endpoint, "v1/logs", headers)),
                MaxQueueSize,
                ScheduledDelayMs,
                ExportTimeoutMs,
                MaxExportBatchSize)),
            tracing => tracing.AddProcessor(new BatchActivityExportProcessor(
                new OtlpTraceExporter(ExporterOptions(endpoint, "v1/traces", headers)),
                MaxQueueSize,
                ScheduledDelayMs,
                ExportTimeoutMs,
                MaxExportBatchSize)));
    }

    /// <summary>Sends what is queued, waiting at most a couple of seconds, then stops. Safe to call more than once.</summary>
    public void Dispose()
    {
        // Logs first: when time is short, the error that ended the session matters more than its spans.
        this.sdk.LoggerProvider.Shutdown(LogShutdownTimeoutMs);
        this.sdk.TracerProvider.Shutdown(TraceShutdownTimeoutMs);
        this.sdkLoggerFactory.Dispose();
        this.sdk.Dispose();
    }

    private static OtlpExporterOptions ExporterOptions(Uri endpoint, string signalPath, string? headers)
    {
        OtlpExporterOptions options = new OtlpExporterOptions
        {
            // Set in code, the endpoint is used verbatim, so it carries the signal path itself.
            Endpoint = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/" + signalPath),
            Protocol = OtlpExportProtocol.HttpProtobuf,
            TimeoutMilliseconds = ExportTimeoutMs,
        };

        if (!string.IsNullOrWhiteSpace(headers))
        {
            options.Headers = headers;
        }

        return options;
    }

    private static void ConfigureResource(ResourceBuilder resource, TelemetryIdentity identity, string environment)
    {
        int processId;
        using (Process process = Process.GetCurrentProcess())
        {
            processId = process.Id;
        }

        resource
            // No service.instance.id: Loki makes it an index label, and one per app start would mean a
            // new stream per session on every desktop. session.id carries that on each record instead.
            .AddService(identity.ServiceName, serviceVersion: identity.ServiceVersion, autoGenerateServiceInstanceId: false)
            .AddAttributes(new Dictionary<string, object>
            {
                [TelemetryAttributes.DeploymentEnvironment] = environment,
                [TelemetryAttributes.HostName] = identity.HostName,
                [TelemetryAttributes.OsType] = "windows",
                [TelemetryAttributes.OsDescription] = RuntimeInformation.OSDescription.Trim(),
                [TelemetryAttributes.RuntimeDescription] = RuntimeInformation.FrameworkDescription,
                [TelemetryAttributes.ProcessId] = processId,
            });
    }
}
