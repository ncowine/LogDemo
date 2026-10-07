using System;
using System.ComponentModel;
using System.IO;
using LogDemo.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;

namespace LogDemo.App.Tests;

public sealed class OpenLogFolderCommandTests
{
    private readonly LogSession session = new LogSession(
        "abc", Path.Combine(Path.GetTempPath(), "LogDemo.App.Tests"), "App", new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero), processId: 999);

    private Mock<IShellLauncher> shell = null!;
    private Mock<IUserNotificationService> notifications = null!;
    private FakeLogger<OpenLogFolderCommand> logger = null!;
    private OpenLogFolderCommand command = null!;

    [SetUp]
    public void SetUp()
    {
        this.shell = new Mock<IShellLauncher>();
        this.notifications = new Mock<IUserNotificationService>();
        this.logger = new FakeLogger<OpenLogFolderCommand>();
        this.command = new OpenLogFolderCommand(this.session, this.shell.Object, this.notifications.Object, this.logger);
    }

    [Test]
    public void Reveals_the_current_log_file()
    {
        this.command.Execute();

        this.shell.Verify(s => s.RevealFile(this.session.LogFilePath), Times.Once);
        Assert.That(this.logger.Collector.GetSnapshot(), Has.Some.Matches<FakeLogRecord>(r => r?.Id.Id == 4000));
    }

    [Test]
    public void Explorer_failing_to_start_tells_the_user_where_the_logs_are()
    {
        this.shell.Setup(s => s.RevealFile(It.IsAny<string>())).Throws(new Win32Exception(2));

        this.command.Execute(); // handled: must not throw

        this.notifications.Verify(n => n.ShowError(It.Is<string>(m => m.Contains(this.session.LogDirectory))), Times.Once);
    }

    [Test]
    public void Unexpected_failure_is_rethrown()
    {
        this.shell.Setup(s => s.RevealFile(It.IsAny<string>())).Throws(new InvalidOperationException("bug"));

        Assert.Throws<InvalidOperationException>(() => this.command.Execute());

        this.notifications.Verify(n => n.ShowError(It.IsAny<string>()), Times.Never);
    }
}
