using System;

namespace LogDemo.Logging.Observability;

/// <summary>What every exported record says about where it came from: app, version, machine, user, run.</summary>
internal sealed class TelemetryIdentity
{
    public TelemetryIdentity(string serviceName, string serviceVersion, string sessionId, string userName, string hostName)
    {
        ServiceName = serviceName;
        ServiceVersion = serviceVersion;
        SessionId = sessionId;
        UserName = userName;
        HostName = hostName;
    }

    public string ServiceName { get; }

    public string ServiceVersion { get; }

    public string SessionId { get; }

    /// <summary><c>DOMAIN\user</c>, as reported by Windows for the signed-in user.</summary>
    public string UserName { get; }

    public string HostName { get; }

    public static TelemetryIdentity ForCurrentUser(string serviceName, string serviceVersion, string sessionId) =>
        new TelemetryIdentity(
            serviceName,
            serviceVersion,
            sessionId,
            Environment.UserDomainName + "\\" + Environment.UserName,
            Environment.MachineName);
}
