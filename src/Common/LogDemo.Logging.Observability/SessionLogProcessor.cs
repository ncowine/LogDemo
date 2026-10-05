using System.Collections.Generic;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace LogDemo.Logging.Observability;

/// <summary>
/// Adds who and which run to every exported log record: <c>user.name</c> and <c>session.id</c>.
/// </summary>
/// <remarks>
/// These are log attributes, not resource attributes, on purpose: Loki turns some resource attributes
/// into index labels, and a label per user or per session would create a stream for each. As log
/// attributes they land in Loki's structured metadata and are still queryable,
/// e.g. <c>{service_name="LogDemo-Net"} | user_name="CONTOSO\\jsmith"</c>.
/// </remarks>
internal sealed class SessionLogProcessor : BaseProcessor<LogRecord>
{
    private readonly KeyValuePair<string, object?>[] attributes;

    public SessionLogProcessor(string userName, string sessionId)
    {
        this.attributes = new[]
        {
            new KeyValuePair<string, object?>(TelemetryAttributes.UserName, userName),
            new KeyValuePair<string, object?>(TelemetryAttributes.SessionId, sessionId),
        };
    }

    // Runs before the export processor, which is added after this one.
    public override void OnEnd(LogRecord data)
    {
        IReadOnlyList<KeyValuePair<string, object?>>? existing = data.Attributes;
        List<KeyValuePair<string, object?>> combined = new List<KeyValuePair<string, object?>>((existing?.Count ?? 0) + this.attributes.Length);
        if (existing is not null)
        {
            combined.AddRange(existing);
        }

        combined.AddRange(this.attributes);
        data.Attributes = combined;
    }
}
