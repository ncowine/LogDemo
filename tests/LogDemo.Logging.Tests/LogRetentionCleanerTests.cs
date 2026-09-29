using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LogDemo.Logging.Tests;

public sealed class LogRetentionCleanerTests : IDisposable
{
    private static readonly DateTime Now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDirectory dir = new TempDirectory();
    private readonly LogRetentionCleaner cleaner = new LogRetentionCleaner(NullLogger<LogRetentionCleaner>.Instance);

    public void Dispose() => this.dir.Dispose();

    [Fact]
    public void Deletes_files_older_than_retention_and_keeps_recent_ones()
    {
        string expired = this.dir.CreateFile("App_20260920_080000_1.log", Now.AddDays(-9));
        string recent = this.dir.CreateFile("App_20260927_080000_2.log", Now.AddDays(-2));

        RetentionResult result = this.cleaner.Clean(CreateSession(), retentionDays: 7, maxRetainedFiles: 0, Now);

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(recent));
        Assert.Equal(1, result.Deleted);
    }

    [Fact]
    public void Never_deletes_the_current_session_including_rolled_parts()
    {
        LogSession session = CreateSession();
        string current = this.dir.CreateFile(session.FileBaseName + ".log", Now.AddDays(-30));
        string rolled = this.dir.CreateFile(session.FileBaseName + "_001.log", Now.AddDays(-30));

        this.cleaner.Clean(session, retentionDays: 1, maxRetainedFiles: 1, Now);

        Assert.True(File.Exists(current));
        Assert.True(File.Exists(rolled));
    }

    [Fact]
    public void Ignores_files_of_other_applications_sharing_the_folder()
    {
        string otherApp = this.dir.CreateFile("AppFx_20260101_080000_1.log", Now.AddDays(-200));
        string unrelated = this.dir.CreateFile("readme.txt", Now.AddDays(-200));

        this.cleaner.Clean(CreateSession(), retentionDays: 7, maxRetainedFiles: 0, Now);

        Assert.True(File.Exists(otherApp));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public void Keeps_only_the_newest_files_when_over_the_count_limit()
    {
        string newest = this.dir.CreateFile("App_20260929_080000_3.log", Now.AddHours(-1));
        string middle = this.dir.CreateFile("App_20260928_080000_2.log", Now.AddDays(-1));
        string oldest = this.dir.CreateFile("App_20260927_080000_1.log", Now.AddDays(-2));

        this.cleaner.Clean(CreateSession(), retentionDays: 7, maxRetainedFiles: 2, Now);

        Assert.True(File.Exists(newest));
        Assert.True(File.Exists(middle));
        Assert.False(File.Exists(oldest));
    }

    [Fact]
    public void Missing_directory_is_not_an_error()
    {
        LogSession session = new LogSession("s", Path.Combine(this.dir.Path, "missing"), "App", DateTimeOffset.Now, 1);

        RetentionResult result = this.cleaner.Clean(session, 7, 0, Now);

        Assert.Equal(0, result.Scanned);
    }

    private LogSession CreateSession() =>
        new LogSession("abc", this.dir.Path, "App", new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero), processId: 999);
}
