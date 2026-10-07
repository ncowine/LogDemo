using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace LogDemo.Logging.Tests;

public sealed class LogRetentionCleanerTests
{
    private static readonly DateTime Now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private readonly LogRetentionCleaner cleaner = new LogRetentionCleaner(NullLogger<LogRetentionCleaner>.Instance);
    private TempDirectory dir = null!;

    [SetUp]
    public void SetUp() => this.dir = new TempDirectory();

    [TearDown]
    public void TearDown() => this.dir.Dispose();

    [Test]
    public void Deletes_files_older_than_retention_and_keeps_recent_ones()
    {
        string expired = this.dir.CreateFile("App_20260920_080000_1.log", Now.AddDays(-9));
        string recent = this.dir.CreateFile("App_20260927_080000_2.log", Now.AddDays(-2));

        RetentionResult result = this.cleaner.Clean(CreateSession(), retentionDays: 7, maxRetainedFiles: 0, Now);

        Assert.That(File.Exists(expired), Is.False);
        Assert.That(File.Exists(recent), Is.True);
        Assert.That(result.Deleted, Is.EqualTo(1));
    }

    [Test]
    public void Never_deletes_the_current_session_including_rolled_parts()
    {
        LogSession session = CreateSession();
        string current = this.dir.CreateFile(session.FileBaseName + ".log", Now.AddDays(-30));
        string rolled = this.dir.CreateFile(session.FileBaseName + "_001.log", Now.AddDays(-30));

        this.cleaner.Clean(session, retentionDays: 1, maxRetainedFiles: 1, Now);

        Assert.That(File.Exists(current), Is.True);
        Assert.That(File.Exists(rolled), Is.True);
    }

    [Test]
    public void Ignores_files_of_other_applications_sharing_the_folder()
    {
        string otherApp = this.dir.CreateFile("AppFx_20260101_080000_1.log", Now.AddDays(-200));
        string unrelated = this.dir.CreateFile("readme.txt", Now.AddDays(-200));

        this.cleaner.Clean(CreateSession(), retentionDays: 7, maxRetainedFiles: 0, Now);

        Assert.That(File.Exists(otherApp), Is.True);
        Assert.That(File.Exists(unrelated), Is.True);
    }

    [Test]
    public void Keeps_only_the_newest_files_when_over_the_count_limit()
    {
        string newest = this.dir.CreateFile("App_20260929_080000_3.log", Now.AddHours(-1));
        string middle = this.dir.CreateFile("App_20260928_080000_2.log", Now.AddDays(-1));
        string oldest = this.dir.CreateFile("App_20260927_080000_1.log", Now.AddDays(-2));

        this.cleaner.Clean(CreateSession(), retentionDays: 7, maxRetainedFiles: 2, Now);

        Assert.That(File.Exists(newest), Is.True);
        Assert.That(File.Exists(middle), Is.True);
        Assert.That(File.Exists(oldest), Is.False);
    }

    [Test]
    public void Missing_directory_is_not_an_error()
    {
        LogSession session = new LogSession("s", Path.Combine(this.dir.Path, "missing"), "App", DateTimeOffset.Now, 1);

        RetentionResult result = this.cleaner.Clean(session, 7, 0, Now);

        Assert.That(result.Scanned, Is.EqualTo(0));
    }

    private LogSession CreateSession() =>
        new LogSession("abc", this.dir.Path, "App", new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero), processId: 999);
}
