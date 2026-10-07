using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;

namespace LogDemo.App.Tests;

public sealed class WriteSampleLogsCommandTests
{
    [Test]
    public void Writes_one_entry_per_level()
    {
        FakeLogger<WriteSampleLogsCommand> logger = new FakeLogger<WriteSampleLogsCommand>();

        new WriteSampleLogsCommand(logger).Execute();

        IReadOnlyList<FakeLogRecord> records = logger.Collector.GetSnapshot();
        LogLevel[] expected = { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
        Assert.That(expected, Is.SubsetOf(records.Select(r => r.Level)));
    }

    [Test]
    public void Error_sample_carries_the_exception_and_the_batch_scope()
    {
        FakeLogger<WriteSampleLogsCommand> logger = new FakeLogger<WriteSampleLogsCommand>();

        new WriteSampleLogsCommand(logger).Execute();

        FakeLogRecord error = logger.Collector.GetSnapshot().Single(r => r.Level == LogLevel.Error);
        Assert.That(error.Exception, Is.TypeOf<InvalidOperationException>());
        Assert.That(error.Scopes, Has.Some.Matches<object?>(s => s is IEnumerable<KeyValuePair<string, object>> pairs
            && pairs.Any(p => p.Key == "BatchId" && Equals(p.Value, "B-7"))));
    }
}
