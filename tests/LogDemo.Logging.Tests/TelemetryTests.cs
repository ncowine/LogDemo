using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LogDemo.Logging.Observability;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace LogDemo.Logging.Tests;

/// <summary>ActivitySource listeners are process-wide, so these tests must not overlap with each other.</summary>
[NonParallelizable]
public sealed class TelemetryTests
{
    private static readonly TelemetryIdentity Identity = new TelemetryIdentity("LogDemo-Test", "1.2.3", "abcd1234", @"CONTOSO\jsmith", "PC-042");

    [Test]
    public void Exported_logs_carry_user_session_scopes_and_resource()
    {
        CapturingLogExporter logs = new CapturingLogExporter();

        using (Pipeline pipeline = new Pipeline(logs, new List<Activity>()))
        {
            ILogger logger = pipeline.LoggerFactory.CreateLogger("App.Component");
            using (logger.BeginScope("{CommandName}#{CommandId}", "SaveCommand", "c0ffee"))
            {
                logger.LogWarning("disk almost full");
            }
        }

        Assert.That(logs.Records, Has.Count.EqualTo(1));
        ExportedLog log = logs.Records[0];
        Assert.That(log.Message, Is.EqualTo("disk almost full"));
        Assert.That(log.Attributes["user.name"], Is.EqualTo(@"CONTOSO\jsmith"));
        Assert.That(log.Attributes["session.id"], Is.EqualTo("abcd1234"));
        Assert.That(log.Scopes["CommandName"], Is.EqualTo("SaveCommand"));

        Assert.That(log.Resource["service.name"], Is.EqualTo("LogDemo-Test"));
        Assert.That(log.Resource["service.version"], Is.EqualTo("1.2.3"));
        Assert.That(log.Resource["deployment.environment"], Is.EqualTo("test"));
        Assert.That(log.Resource["host.name"], Is.EqualTo("PC-042"));
        // One per app start would give Loki a new stream per session on every desktop.
        Assert.That(log.Resource.ContainsKey("service.instance.id"), Is.False);
    }

    [Test]
    public void Logging_OpenTelemetry_section_filters_the_export_only()
    {
        CapturingLogExporter logs = new CapturingLogExporter();
        Dictionary<string, string?> config = new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Debug",
            ["Logging:OpenTelemetry:LogLevel:Default"] = "Warning",
            ["Logging:OpenTelemetry:LogLevel:Chatty"] = "Debug",
        };

        using (Pipeline pipeline = new Pipeline(logs, new List<Activity>(), config))
        {
            pipeline.LoggerFactory.CreateLogger("App").LogInformation("info-not-exported");
            pipeline.LoggerFactory.CreateLogger("App").LogError("error-exported");
            pipeline.LoggerFactory.CreateLogger("Chatty.Component").LogDebug("debug-exported");
        }

        Assert.That(logs.Records.Select(r => r.Message), Is.EqualTo(new[] { "error-exported", "debug-exported" }));
    }

    [Test]
    public void Logs_written_during_a_span_carry_its_trace_id_and_the_span_says_who()
    {
        CapturingLogExporter logs = new CapturingLogExporter();
        List<Activity> spans = new List<Activity>();

        ActivityTraceId traceId;
        using (Pipeline pipeline = new Pipeline(logs, spans))
        {
            using (Activity? activity = AppTracing.Source.StartActivity("LoadCustomers"))
            {
                Assert.That(activity, Is.Not.Null);
                traceId = activity!.TraceId;
                pipeline.LoggerFactory.CreateLogger("App").LogInformation("inside the span");
            }
        }

        Assert.That(logs.Records, Has.Count.EqualTo(1));
        Assert.That(logs.Records[0].TraceId, Is.EqualTo(traceId));
        Activity span = spans.Single(s => s.TraceId == traceId);
        Assert.That(span.GetTagItem("user.name"), Is.EqualTo(@"CONTOSO\jsmith"));
        Assert.That(span.GetTagItem("session.id"), Is.EqualTo("abcd1234"));
    }

    [Test]
    public async Task A_failing_command_is_an_error_span_and_its_logs_share_the_trace()
    {
        CapturingLogExporter logs = new CapturingLogExporter();
        List<Activity> spans = new List<Activity>();

        using (Pipeline pipeline = new Pipeline(logs, spans))
        {
            FailingCommand command = new FailingCommand(pipeline.LoggerFactory.CreateLogger<FailingCommand>());
            await command.ExecuteAsync(null);
        }

        Activity span = spans.Single(s => s.DisplayName == nameof(FailingCommand));
        Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
        Assert.That(span.StatusDescription, Is.EqualTo("backend down"));
        Assert.That(span.GetTagItem("command.id"), Is.Not.Null);
        Assert.That(logs.Records, Has.Some.Matches<ExportedLog>(r => r?.TraceId == span.TraceId && r.Level == LogLevel.Warning));
    }

    /// <summary>What the collector receives: OTLP/HTTP protobuf on the standard signal paths.</summary>
    [Test]
    public async Task LoggingHost_posts_logs_and_traces_to_the_collector_paths()
    {
        int port = FreePort();
        List<string> received = new List<string>();
        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        Task receiving = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException)
                {
                    return;
                }

                lock (received)
                {
                    received.Add($"{context.Request.HttpMethod} {context.Request.Url!.AbsolutePath} {context.Request.ContentType}");
                }

                context.Response.StatusCode = 200;
                context.Response.Close();
            }
        });

        using TempDirectory dir = new TempDirectory();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileLogging:Directory"] = dir.Path,
            ["Telemetry:Endpoint"] = $"http://localhost:{port}",
        }).Build();

        using (LoggingHost host = LoggingHost.Create(config, "App", dir.Path, "1.0.0", OpenTelemetryExport.FromConfiguration))
        using (AppTracing.Source.StartActivity("Work"))
        {
            host.LoggerFactory.CreateLogger("App").LogWarning("to the collector");
        }

        listener.Stop();
        await receiving;

        Assert.That(received, Does.Contain("POST /v1/logs application/x-protobuf"));
        Assert.That(received, Does.Contain("POST /v1/traces application/x-protobuf"));
    }

    [Test]
    public void Shutdown_with_an_unreachable_collector_is_bounded_and_does_not_throw()
    {
        using TempDirectory dir = new TempDirectory();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileLogging:Directory"] = dir.Path,
            ["Telemetry:Endpoint"] = "http://127.0.0.1:9", // discard port, nothing listens
        }).Build();

        OpenTelemetryExport? export = null;
        LoggingHost host = LoggingHost.Create(config, "App", dir.Path, "1.0.0", context => export = OpenTelemetryExport.FromConfiguration(context));
        host.LoggerFactory.CreateLogger("App").LogError("nobody will receive this");

        Stopwatch stopwatch = Stopwatch.StartNew();
        host.Dispose();

        Assert.That(export!.Options.IsEnabled, Is.True);
        Assert.That(stopwatch.Elapsed < TimeSpan.FromSeconds(5), Is.True, $"Dispose took {stopwatch.Elapsed}");
    }

    [Test]
    public void Without_the_extension_nothing_is_exported_even_when_configured()
    {
        using TempDirectory dir = new TempDirectory();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileLogging:Directory"] = dir.Path,
            ["Telemetry:Endpoint"] = "http://127.0.0.1:9",
        }).Build();

        using (LoggingHost host = LoggingHost.Create(config, "App", dir.Path, "1.0.0"))
        {
            // No listener on the source: the core alone never creates spans.
            Assert.That(AppTracing.Source.StartActivity("Work"), Is.Null);
        }
    }

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("http://otel.contoso.local:4318/", "http://otel.contoso.local:4318/")]
    [TestCase("https://otel.contoso.local", "https://otel.contoso.local/")]
    public void Valid_or_empty_endpoints_are_accepted_silently(string? endpoint, string? expected)
    {
        TelemetryOptions options = new TelemetryOptions { Endpoint = endpoint };

        IReadOnlyList<string> warnings = options.Normalize();

        Assert.That(warnings, Is.Empty);
        Assert.That(options.EndpointUri?.AbsoluteUri, Is.EqualTo(expected));
        Assert.That(options.IsEnabled, Is.EqualTo(expected is not null));
    }

    [TestCase("otel.contoso.local:4318")]
    [TestCase("ftp://otel.contoso.local")]
    [TestCase("not a url")]
    public void Invalid_endpoint_turns_export_off_with_a_warning(string endpoint)
    {
        TelemetryOptions options = new TelemetryOptions { Endpoint = endpoint, Environment = " " };

        IReadOnlyList<string> warnings = options.Normalize();

        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain(endpoint));
        Assert.That(options.IsEnabled, Is.False);
        Assert.That(options.Environment, Is.EqualTo("production"));
    }

    [TestCase("api-key")]
    [TestCase("api-key=secret-token,other")]
    [TestCase("=secret-token")]
    public void Malformed_headers_turn_export_off_without_repeating_the_value(string headers)
    {
        TelemetryOptions options = new TelemetryOptions { Endpoint = "http://otel.contoso.local:4318", Headers = headers };

        IReadOnlyList<string> warnings = options.Normalize();

        Assert.That(warnings, Has.Count.EqualTo(1));
        string warning = warnings[0];
        Assert.That(warning, Does.Contain("Telemetry:Headers"));
        Assert.That(warning, Does.Not.Contain("secret-token"));
        Assert.That(options.IsEnabled, Is.False);
    }

    [Test]
    public void Well_formed_headers_are_accepted()
    {
        TelemetryOptions options = new TelemetryOptions { Endpoint = "http://otel.contoso.local:4318", Headers = "api-key=abc==, x-tenant = 42" };

        Assert.That(options.Normalize(), Is.Empty);
        Assert.That(options.IsEnabled, Is.True);
    }

    /// <summary>A typo in the Telemetry section must neither stop the app nor be misreported in the header.</summary>
    [TestCase("http://127.0.0.1:9", "api-key")]
    [TestCase("otel.contoso.local:4318", null)]
    public void Invalid_telemetry_configuration_starts_with_file_logging_and_says_why(string endpoint, string? headers)
    {
        using TempDirectory dir = new TempDirectory();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileLogging:Directory"] = dir.Path,
            ["Telemetry:Endpoint"] = endpoint,
            ["Telemetry:Headers"] = headers,
        }).Build();

        string path;
        using (LoggingHost host = LoggingHost.Create(config, "App", dir.Path, "1.0.0", OpenTelemetryExport.FromConfiguration))
        {
            host.WriteSessionHeader("App", typeof(TelemetryTests).Assembly);
            path = host.Session.LogFilePath;
        }

        string content = File.ReadAllText(path);
        Assert.That(content, Does.Contain("Logging configuration problem: Telemetry:"));
        Assert.That(content, Does.Contain("Telemetry configuration is invalid"));
        Assert.That(content, Does.Not.Contain("Telemetry:Endpoint is empty"));
    }

    private static int FreePort()
    {
        TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>The real pipeline with in-memory exporters in place of OTLP, plugged into a factory like the host's.</summary>
    private sealed class Pipeline : IDisposable
    {
        private readonly TelemetryPipeline telemetry;

        public Pipeline(CapturingLogExporter logs, List<Activity> spans, IDictionary<string, string?>? config = null)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(config ?? new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Information" })
                .Build();

            this.telemetry = new TelemetryPipeline(
                Identity,
                "test",
                logging => logging.AddProcessor(new SimpleLogRecordExportProcessor(logs)),
                tracing => tracing.AddInMemoryExporter(spans));

            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddProvider(this.telemetry.LoggerProvider);
            });
        }

        public ILoggerFactory LoggerFactory { get; }

        public void Dispose()
        {
            LoggerFactory.Dispose();
            this.telemetry.Dispose();
        }
    }

    /// <summary>Copies what the OTLP exporter would send; LogRecord instances are pooled and reused after export.</summary>
    private sealed class CapturingLogExporter : BaseExporter<LogRecord>
    {
        public List<ExportedLog> Records { get; } = new List<ExportedLog>();

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            Dictionary<string, object?> resource = ParentProvider!.GetResource().Attributes.ToDictionary(a => a.Key, a => (object?)a.Value);
            foreach (LogRecord record in batch)
            {
                Dictionary<string, object?> scopes = new Dictionary<string, object?>();
                record.ForEachScope(
                    (scope, state) =>
                    {
                        foreach (KeyValuePair<string, object?> pair in scope)
                        {
                            state[pair.Key] = pair.Value;
                        }
                    },
                    scopes);

                Records.Add(new ExportedLog(
                    record.FormattedMessage,
                    record.LogLevel,
                    record.TraceId,
                    (record.Attributes ?? Array.Empty<KeyValuePair<string, object?>>()).ToDictionary(a => a.Key, a => a.Value),
                    scopes,
                    resource));
            }

            return ExportResult.Success;
        }
    }

    private sealed class ExportedLog
    {
        public ExportedLog(
            string? message,
            LogLevel level,
            ActivityTraceId traceId,
            Dictionary<string, object?> attributes,
            Dictionary<string, object?> scopes,
            Dictionary<string, object?> resource)
        {
            Message = message;
            Level = level;
            TraceId = traceId;
            Attributes = attributes;
            Scopes = scopes;
            Resource = resource;
        }

        public string? Message { get; }

        public LogLevel Level { get; }

        public ActivityTraceId TraceId { get; }

        public Dictionary<string, object?> Attributes { get; }

        public Dictionary<string, object?> Scopes { get; }

        public Dictionary<string, object?> Resource { get; }
    }

    private sealed class FailingCommand : DelegateBaseAsyncCommand
    {
        public FailingCommand(ILogger logger)
            : base(logger)
        {
        }

        protected override Task InvokeAsync(CancellationToken cancellationToken) =>
            throw new TimeoutException("backend down");

        protected override bool TryHandleFailure(Exception exception) => exception is TimeoutException;
    }
}
