using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LogDemo.Logging.Tests;

public sealed class LoggingHostTests
{
    [Fact]
    public void Writes_a_new_file_per_session_named_after_the_prefix()
    {
        using TempDirectory dir = new TempDirectory();
        IConfiguration config = Configuration(dir.Path);

        string first;
        string second;
        using (LoggingHost host = LoggingHost.Create(config, "My_App", dir.Path))
        {
            host.LoggerFactory.CreateLogger("Test").LogInformation("hello from session one");
            first = host.Session.LogFilePath;
        }

        using (LoggingHost host = LoggingHost.Create(config, "My_App", dir.Path))
        {
            second = host.Session.LogFilePath;
        }

        Assert.NotEqual(first, second);
        Assert.StartsWith("My-App_", Path.GetFileName(first)); // '_' is reserved as separator
        Assert.Contains("hello from session one", File.ReadAllText(first));
    }

    /// <summary>
    /// Regression: Serilog's AddSerilog() adds a Trace rule for its provider that silently
    /// overrides Logging:LogLevel. The configured levels must be what reaches the file.
    /// </summary>
    [Fact]
    public void Honours_Logging_LogLevel_configuration()
    {
        using TempDirectory dir = new TempDirectory();
        IConfiguration config = Configuration(dir.Path, new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:Noisy"] = "Warning",
        });

        string path;
        using (LoggingHost host = LoggingHost.Create(config, "App", dir.Path))
        {
            ILogger app = host.LoggerFactory.CreateLogger("App.Component");
            ILogger noisy = host.LoggerFactory.CreateLogger("Noisy.Component");
            app.LogDebug("debug-should-be-filtered");
            app.LogInformation("info-should-be-written");
            noisy.LogInformation("noisy-info-should-be-filtered");
            noisy.LogWarning("noisy-warning-should-be-written");
            path = host.Session.LogFilePath;
        }

        string content = File.ReadAllText(path);
        Assert.DoesNotContain("debug-should-be-filtered", content);
        Assert.Contains("info-should-be-written", content);
        Assert.DoesNotContain("noisy-info-should-be-filtered", content);
        Assert.Contains("noisy-warning-should-be-written", content);
    }

    [Fact]
    public void Invalid_settings_are_clamped_and_reported_instead_of_throwing()
    {
        using TempDirectory dir = new TempDirectory();
        IConfiguration config = Configuration(dir.Path, new Dictionary<string, string?>
        {
            ["FileLogging:RetentionDays"] = "0",
            ["FileLogging:MaxFileSizeMB"] = "-5",
        });

        string path;
        using (LoggingHost host = LoggingHost.Create(config, "App", dir.Path))
        {
            Assert.Equal(1, host.Options.RetentionDays);
            Assert.Equal(20, host.Options.MaxFileSizeMB);
            path = host.Session.LogFilePath;
        }

        string content = File.ReadAllText(path);
        Assert.Contains("RetentionDays=0", content);
        Assert.Contains("MaxFileSizeMB=-5", content);
    }

    [Fact]
    public async Task Retention_runs_in_the_background_and_logs_its_result()
    {
        using TempDirectory dir = new TempDirectory();
        dir.CreateFile("App_20200101_000000_1.log", new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));

        string path;
        using (LoggingHost host = LoggingHost.Create(Configuration(dir.Path), "App", dir.Path))
        {
            await host.StartRetentionCleanup();
            path = host.Session.LogFilePath;
        }

        Assert.Single(Directory.GetFiles(dir.Path, "App_*.log"));
        Assert.Contains("deleted 1", File.ReadLines(path).Single(l => l.Contains("Log retention")));
    }

    [Fact]
    public void Extension_that_cannot_start_is_logged_and_the_app_keeps_its_file_log()
    {
        using TempDirectory dir = new TempDirectory();

        string path;
        using (LoggingHost host = LoggingHost.Create(
            Configuration(dir.Path), "App", dir.Path, "1.0.0", _ => throw new InvalidOperationException("exporter broken")))
        {
            host.LoggerFactory.CreateLogger("App").LogInformation("still logging");
            path = host.Session.LogFilePath;
        }

        string content = File.ReadAllText(path);
        Assert.Contains("Logging extension could not be started", content);
        Assert.Contains("exporter broken", content);
        Assert.Contains("still logging", content);
    }

    [Fact]
    public void Extension_failing_while_the_factory_is_built_is_cleaned_up_and_rethrown()
    {
        using TempDirectory dir = new TempDirectory();
        ThrowingExtension extension = new ThrowingExtension();

        Assert.Throws<InvalidOperationException>(() => LoggingHost.Create(Configuration(dir.Path), "App", dir.Path, "1.0.0", _ => extension));

        Assert.True(extension.Disposed);
        foreach (string file in Directory.GetFiles(dir.Path, "App_*.log"))
        {
            File.Delete(file); // throws if the session file were still open
        }
    }

    private static IConfiguration Configuration(string directory, IDictionary<string, string?>? values = null)
    {
        Dictionary<string, string?> all = new Dictionary<string, string?> { ["FileLogging:Directory"] = directory };
        foreach (KeyValuePair<string, string?> pair in values ?? new Dictionary<string, string?>())
        {
            all[pair.Key] = pair.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(all).Build();
    }

    private sealed class ThrowingExtension : ILoggingHostExtension
    {
        public bool Disposed { get; private set; }

        public void ConfigureLogging(ILoggingBuilder logging) => throw new InvalidOperationException("provider broken");

        public void WriteSessionHeader(ILogger logger)
        {
        }

        public void Dispose() => Disposed = true;
    }
}
