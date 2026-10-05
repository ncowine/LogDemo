using System;
using System.Collections.Generic;

namespace LogDemo.Logging.Observability;

/// <summary>
/// Central export of logs and traces over OTLP/HTTP to the OpenTelemetry Collector, bound from the
/// <c>Telemetry</c> configuration section. Applied at startup.
/// </summary>
/// <remarks>
/// Which log levels are exported is not configured here but with the standard provider-specific
/// section <c>Logging:OpenTelemetry:LogLevel</c>, which reloads while the app is running. Without that
/// section the export uses the same <c>Logging:LogLevel</c> rules as the file.
/// </remarks>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    internal const string DefaultEnvironment = "production";

    /// <summary>
    /// Base address of the collector's OTLP/HTTP receiver, e.g. <c>http://otel.contoso.local:4318</c>.
    /// <c>/v1/logs</c> and <c>/v1/traces</c> are appended. Empty turns central export off; the file log
    /// is written either way.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Sent as <c>deployment.environment</c>, e.g. <c>production</c>, <c>test</c>, <c>development</c>.</summary>
    public string Environment { get; set; } = DefaultEnvironment;

    /// <summary>
    /// Optional headers for every export, <c>key=value,key2=value2</c>. Only needed when the collector
    /// checks one. Anything shipped with a desktop app is readable by its users, so treat it as a
    /// filter against accidental traffic, not as a secret.
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>The validated <see cref="Endpoint"/>; <c>null</c> when export is off.</summary>
    public Uri? EndpointUri { get; private set; }

    public bool IsEnabled => EndpointUri is not null;

    /// <summary>
    /// Validates instead of throwing: a typo must not stop the app from starting. An unusable endpoint
    /// turns export off and is reported once the logger exists.
    /// </summary>
    internal IReadOnlyList<string> Normalize()
    {
        List<string> warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(Environment))
        {
            Environment = DefaultEnvironment;
        }

        EndpointUri = null;
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            return warnings;
        }

        if (Uri.TryCreate(Endpoint!.Trim().TrimEnd('/'), UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            EndpointUri = uri;
        }
        else
        {
            warnings.Add($"{SectionName}:{nameof(Endpoint)}={Endpoint} is not an absolute http(s) URL, central export is off.");
        }

        // The OTLP exporter throws on a malformed value while it is being built, i.e. during app startup.
        // The value itself is not repeated in the warning: it may hold a key.
        if (EndpointUri is not null && !string.IsNullOrWhiteSpace(Headers) && !AreValidHeaders(Headers!))
        {
            EndpointUri = null;
            warnings.Add($"{SectionName}:{nameof(Headers)} is not in the form key=value,key2=value2, central export is off.");
        }

        return warnings;
    }

    private static bool AreValidHeaders(string headers)
    {
        foreach (string pair in headers.Split(','))
        {
            int separator = pair.IndexOf('=');
            if (separator < 0 || pair.Substring(0, separator).Trim().Length == 0)
            {
                return false;
            }
        }

        return true;
    }
}
