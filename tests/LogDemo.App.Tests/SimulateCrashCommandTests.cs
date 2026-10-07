using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;

namespace LogDemo.App.Tests;

/// <summary>The worker-thread crash is only tested up to the confirmation: going further would end the test run.</summary>
public sealed class SimulateCrashCommandTests
{
    private Mock<IUserNotificationService> notifications = null!;
    private FakeLogger<SimulateCrashCommand> logger = null!;
    private SimulateCrashCommand command = null!;

    [SetUp]
    public void SetUp()
    {
        this.notifications = new Mock<IUserNotificationService>();
        this.logger = new FakeLogger<SimulateCrashCommand>();
        this.command = new SimulateCrashCommand(this.notifications.Object, this.logger);
    }

    [TestCase(SimulateCrashCommand.UiThread, true)]
    [TestCase(SimulateCrashCommand.WorkerThread, true)]
    [TestCase("ui", false)]
    [TestCase("", false)]
    public void Accepts_only_the_known_targets(string target, bool expected)
    {
        Assert.That(this.command.CanExecute(target), Is.EqualTo(expected));
    }

    [Test]
    public void Ui_thread_crash_is_logged_and_thrown_to_the_global_handler()
    {
        Assert.Throws<InvalidOperationException>(() => this.command.Execute(SimulateCrashCommand.UiThread));

        Assert.That(this.logger.Collector.GetSnapshot(), Has.Some.Matches<FakeLogRecord>(r => r?.Id.Id == 4002));
        Assert.That(this.logger.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
    }

    [Test]
    public void Declining_the_worker_thread_crash_does_nothing()
    {
        this.notifications.Setup(n => n.Confirm(It.IsAny<string>())).Returns(false);

        this.command.Execute(SimulateCrashCommand.WorkerThread);

        this.notifications.Verify(n => n.Confirm(It.IsAny<string>()), Times.Once);
        Assert.That(this.logger.Collector.GetSnapshot(), Has.None.Matches<FakeLogRecord>(r => r?.Id.Id == 4002));
    }
}
