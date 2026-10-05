using System.Diagnostics;
using OpenTelemetry;

namespace LogDemo.Logging.Observability;

/// <summary>Tags every span with <c>user.name</c> and <c>session.id</c>, so a trace in Tempo says who it was.</summary>
internal sealed class SessionSpanProcessor : BaseProcessor<Activity>
{
    private readonly string userName;
    private readonly string sessionId;

    public SessionSpanProcessor(string userName, string sessionId)
    {
        this.userName = userName;
        this.sessionId = sessionId;
    }

    public override void OnStart(Activity data)
    {
        data.SetTag(TelemetryAttributes.UserName, this.userName);
        data.SetTag(TelemetryAttributes.SessionId, this.sessionId);
    }
}
