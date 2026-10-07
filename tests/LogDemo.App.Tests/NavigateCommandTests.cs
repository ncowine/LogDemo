using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;
using Prism.Regions;

namespace LogDemo.App.Tests;

public sealed class NavigateCommandTests
{
    private Mock<IRegionManager> regionManager = null!;
    private FakeLogger<NavigateCommand> logger = null!;
    private NavigateCommand command = null!;

    [SetUp]
    public void SetUp()
    {
        this.regionManager = new Mock<IRegionManager>();
        this.logger = new FakeLogger<NavigateCommand>();
        this.command = new NavigateCommand(this.regionManager.Object, this.logger);
    }

    [TestCase(null)]
    [TestCase("")]
    public void Needs_a_target(string? target)
    {
        Assert.That(this.command.CanExecute(target), Is.False);
    }

    [Test]
    public void Navigates_the_content_region_and_logs_success()
    {
        CompleteNavigationWith(new NavigationResult(null!, true));

        this.command.Execute(ViewNames.Customers);

        this.regionManager.Verify(r => r.RequestNavigate(RegionNames.Content, ViewNames.Customers, It.IsAny<Action<NavigationResult>>()), Times.Once);
        Assert.That(this.logger.Collector.GetSnapshot(), Has.Some.Matches<FakeLogRecord>(r => r?.Id.Id == 3100));
    }

    /// <summary>Prism reports navigation problems through the callback instead of throwing; they must reach the log.</summary>
    [Test]
    public void Failed_navigation_is_logged_as_an_error()
    {
        InvalidOperationException error = new InvalidOperationException("view not registered");
        CompleteNavigationWith(new NavigationResult(null!, error));

        this.command.Execute(ViewNames.Customers);

        FakeLogRecord failed = this.logger.Collector.GetSnapshot().Single(r => r.Id.Id == 3101);
        Assert.That(failed.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(failed.Exception, Is.SameAs(error));
    }

    private void CompleteNavigationWith(NavigationResult result) =>
        this.regionManager
            .Setup(r => r.RequestNavigate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NavigationResult>>()))
            .Callback((string _, string _, Action<NavigationResult> callback) => callback(result));
}
