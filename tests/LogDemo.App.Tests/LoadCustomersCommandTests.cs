using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;

namespace LogDemo.App.Tests;

[NonParallelizable] // CustomersTestContext sets Prism's global ContainerLocator
public sealed class LoadCustomersCommandTests
{
    private CustomersTestContext context = null!;

    [SetUp]
    public void SetUp() => this.context = new CustomersTestContext();

    [TearDown]
    public void TearDown() => this.context.Dispose();

    [Test]
    public async Task Replaces_the_customers_and_logs_the_count()
    {
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CustomersTestContext.Grace });
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.ErrorMessage = "old error";

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.That(viewModel.Customers, Is.EqualTo(new[] { CustomersTestContext.Grace }));
        Assert.That(viewModel.ErrorMessage, Is.Null);
        Assert.That(viewModel.LastRefreshed, Is.Not.Null);
        Assert.That(this.context.RecordsOf<LoadCustomersCommand>(), Has.Some.Matches<FakeLogRecord>(r => r?.Id.Id == 3000 && r.Message == "Loaded 1 customers"));
    }

    [Test]
    public async Task View_model_is_busy_while_loading()
    {
        TaskCompletionSource<IReadOnlyList<Customer>> pending = new TaskCompletionSource<IReadOnlyList<Customer>>();
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .Returns(pending.Task);
        CustomersViewModel viewModel = this.context.CreateViewModel();

        Task loading = viewModel.LoadCommand.ExecuteAsync();

        Assert.That(viewModel.IsBusy, Is.True);
        Assert.That(viewModel.LoadCommand.CanExecute(), Is.False);

        pending.SetResult(new[] { CustomersTestContext.Ada });
        await loading;

        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.LoadCommand.CanExecute(), Is.True);
    }

    [Test]
    public async Task Backend_failure_is_shown_to_the_user_and_not_rethrown()
    {
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CustomerServiceException("The customer service did not respond."));
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();

        await viewModel.LoadCommand.ExecuteAsync();

        Assert.That(viewModel.ErrorMessage, Is.EqualTo(LoadCustomersCommand.LoadFailedMessage));
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.Customers, Has.Count.EqualTo(2)); // the old list stays visible
        Assert.That(this.context.RecordsOf<LoadCustomersCommand>(), Has.Some.Matches<FakeLogRecord>(r => r?.Level == LogLevel.Warning && r.Exception is CustomerServiceException));
    }

    [Test]
    public async Task Unexpected_failure_is_rethrown_and_the_view_model_is_not_left_busy()
    {
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));
        CustomersViewModel viewModel = this.context.CreateViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewModel.LoadCommand.ExecuteAsync());

        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.ErrorMessage, Is.Null); // the global handler informs the user
        Assert.That(this.context.RecordsOf<LoadCustomersCommand>(), Has.Some.Matches<FakeLogRecord>(r => r?.Level == LogLevel.Error));
    }

    [Test]
    public async Task Does_not_start_while_the_view_model_is_busy()
    {
        CustomersViewModel viewModel = this.context.CreateViewModel();
        viewModel.IsBusy = true;

        await viewModel.LoadCommand.ExecuteAsync();

        this.context.CustomerService.Verify(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
