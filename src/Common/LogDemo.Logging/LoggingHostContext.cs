using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace LogDemo.Logging;

/// <summary>What a <see cref="ILoggingHostExtension"/> gets to set itself up, before any logger exists.</summary>
public sealed class LoggingHostContext
{
    private readonly List<string> problems;

    internal LoggingHostContext(
        IConfiguration configuration,
        string applicationName,
        string applicationVersion,
        LogSession session,
        List<string> problems)
    {
        Configuration = configuration;
        ApplicationName = applicationName;
        ApplicationVersion = applicationVersion;
        Session = session;
        this.problems = problems;
    }

    public IConfiguration Configuration { get; }

    public string ApplicationName { get; }

    /// <summary>The version passed to <see cref="LoggingHost.Create"/>, or <c>unknown</c>.</summary>
    public string ApplicationVersion { get; }

    public LogSession Session { get; }

    /// <summary>Logged as a warning once the logger exists. Use instead of throwing: a typo must not stop the app.</summary>
    public void ReportConfigurationProblem(string problem) => this.problems.Add(problem);
}
