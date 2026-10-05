namespace LogDemo.Logging.Observability;

/// <summary>
/// Attribute names, following the OpenTelemetry semantic conventions where one exists. Loki replaces the
/// dots with underscores, so <c>user.name</c> is queried as <c>user_name</c>.
/// </summary>
internal static class TelemetryAttributes
{
    public const string UserName = "user.name";
    public const string SessionId = "session.id";
    public const string HostName = "host.name";
    public const string OsType = "os.type";
    public const string OsDescription = "os.description";
    public const string RuntimeDescription = "process.runtime.description";
    public const string ProcessId = "process.pid";

    /// <summary>
    /// The older name, not <c>deployment.environment.name</c>: this is the one Loki 3.x turns into an index
    /// label by default, which suits a value with only a handful of possible values.
    /// </summary>
    public const string DeploymentEnvironment = "deployment.environment";
}
