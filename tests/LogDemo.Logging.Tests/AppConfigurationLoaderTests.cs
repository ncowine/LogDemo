using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;

namespace LogDemo.Logging.Tests;

public sealed class AppConfigurationLoaderTests
{
    [Test]
    public void Later_sources_override_earlier_ones()
    {
        using TempDirectory dir = new TempDirectory();
        string appSettings = Write(dir, "appsettings.json", "{ \"FileLogging\": { \"RetentionDays\": 7 } }");
        string userSettings = Write(dir, "appsettings.user.json", "{ \"FileLogging\": { \"RetentionDays\": 30 } }");

        IConfigurationRoot configuration = new AppConfigurationLoader().Build(appSettings, userSettings, "LOGDEMO_TEST_");

        Assert.That(configuration["FileLogging:RetentionDays"], Is.EqualTo("30"));
    }

    [Test]
    public void Broken_json_is_ignored_and_logged_once_a_logger_is_attached()
    {
        using TempDirectory dir = new TempDirectory();
        string appSettings = Write(dir, "appsettings.json", "{ \"FileLogging\": { \"RetentionDays\": 7 } }");
        string userSettings = Write(dir, "appsettings.user.json", "{ this is not json");
        AppConfigurationLoader loader = new AppConfigurationLoader();

        IConfigurationRoot configuration = loader.Build(appSettings, userSettings, "LOGDEMO_TEST_"); // must not throw

        Assert.That(configuration["FileLogging:RetentionDays"], Is.EqualTo("7"));

        FakeLogger logger = new FakeLogger();
        loader.AttachLogger(logger);
        IReadOnlyList<FakeLogRecord> records = logger.Collector.GetSnapshot();
        Assert.That(records, Has.Count.EqualTo(1));
        FakeLogRecord record = records[0];
        Assert.That(record.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(record.Id.Id, Is.EqualTo(130));
    }

    private static string Write(TempDirectory dir, string name, string json)
    {
        string path = Path.Combine(dir.Path, name);
        File.WriteAllText(path, json);
        return path;
    }
}
