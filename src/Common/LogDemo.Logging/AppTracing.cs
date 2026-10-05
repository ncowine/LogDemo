using System.Diagnostics;

namespace LogDemo.Logging;

/// <summary>
/// The apps' own spans. Every command execution is one (see <c>BaseCommand</c>); app code can
/// start more for work worth seeing in a trace. Outgoing <c>HttpClient</c> calls become child spans and
/// carry the <c>traceparent</c> header, so a server that also uses OpenTelemetry continues the same trace.
/// </summary>
public static class AppTracing
{
    public const string SourceName = "LogDemo";

    /// <summary>Returns <c>null</c> from <c>StartActivity</c> when nothing is listening (export off), so it costs nothing.</summary>
    public static readonly ActivitySource Source = new ActivitySource(SourceName);
}
