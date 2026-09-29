using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace LogDemo.Logging.Tests;

public sealed class AppConfigurationLoaderTests
{
    [Fact]
    public void Later_sources_override_earlier_ones()
    {
        using TempDirectory dir = new TempDirectory();
        string appSettings = Write(dir, "appsettings.json", "{ \"FileLogging\": { \"RetentionDays\": 7 } }");
        string userSettings = Write(dir, "appsettings.user.json", "{ \"FileLogging\": { \"RetentionDays\": 30 } }");

        IConfigurationRoot configuration = new AppConfigurationLoader().Build(appSettings, userSettings, "LOGDEMO_TEST_");

        Assert.Equal("30", configuration["FileLogging:RetentionDays"]);
    }

    [Fact]
    public void Broken_json_is_ignored_and_logged_once_a_logger_is_attached()
    {
        using TempDirectory dir = new TempDirectory();
        string appSettings = Write(dir, "appsettings.json", "{ \"FileLogging\": { \"RetentionDays\": 7 } }");
        string userSettings = Write(dir, "appsettings.user.json", "{ this is not json");
        AppConfigurationLoader loader = new AppConfigurationLoader();

        IConfigurationRoot configuration = loader.Build(appSettings, userSettings, "LOGDEMO_TEST_"); // must not throw

        Assert.Equal("7", configuration["FileLogging:RetentionDays"]);

        FakeLogger logger = new FakeLogger();
        loader.AttachLogger(logger);
        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(130, record.Id.Id);
    }

    private static string Write(TempDirectory dir, string name, string json)
    {
        string path = Path.Combine(dir.Path, name);
        File.WriteAllText(path, json);
        return path;
    }
}
