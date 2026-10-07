using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;

namespace LogDemo.App.Tests;

[NonParallelizable] // CustomersTestContext sets Prism's global ContainerLocator
public sealed class RemoveCustomerCommandTests
{
    private CustomersTestContext context = null!;

    [SetUp]
    public void SetUp() => this.context = new CustomersTestContext();

    [TearDown]
    public void TearDown() => this.context.Dispose();

    [Test]
    public void Needs_a_selected_customer()
    {
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();

        Assert.That(viewModel.RemoveCommand.CanExecute(), Is.False);
    }

    [Test]
    public async Task Confirmed_removal_removes_the_customer_in_the_backend_and_the_list()
    {
        this.context.Notifications.Setup(n => n.Confirm(It.IsAny<string>())).Returns(true);
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.SelectedCustomer = CustomersTestContext.Ada;

        await viewModel.RemoveCommand.ExecuteAsync();

        this.context.CustomerService.Verify(s => s.RemoveCustomerAsync(CustomersTestContext.Ada.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(viewModel.Customers, Is.EqualTo(new[] { CustomersTestContext.Grace }));
        Assert.That(viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task Asks_for_confirmation_by_name()
    {
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.SelectedCustomer = CustomersTestContext.Ada;

        await viewModel.RemoveCommand.ExecuteAsync();

        this.context.Notifications.Verify(n => n.Confirm(It.Is<string>(m => m.Contains(CustomersTestContext.Ada.Name))), Times.Once);
    }

    [Test]
    public async Task Declined_removal_changes_nothing()
    {
        this.context.Notifications.Setup(n => n.Confirm(It.IsAny<string>())).Returns(false);
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.SelectedCustomer = CustomersTestContext.Ada;

        await viewModel.RemoveCommand.ExecuteAsync();

        this.context.CustomerService.Verify(s => s.RemoveCustomerAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(viewModel.Customers, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Backend_failure_is_shown_to_the_user_and_the_customer_stays()
    {
        this.context.Notifications.Setup(n => n.Confirm(It.IsAny<string>())).Returns(true);
        this.context.CustomerService
            .Setup(s => s.RemoveCustomerAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CustomerServiceException("The customer service did not respond."));
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.SelectedCustomer = CustomersTestContext.Ada;

        await viewModel.RemoveCommand.ExecuteAsync();

        this.context.Notifications.Verify(n => n.ShowError(It.IsAny<string>()), Times.Once);
        Assert.That(viewModel.Customers, Does.Contain(CustomersTestContext.Ada));
        Assert.That(viewModel.IsBusy, Is.False);
    }

    /// <summary>Customers are logged by id only: names and e-mail addresses must never reach a log file.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task Logs_the_customer_id_but_no_personal_data(bool confirmed)
    {
        this.context.Notifications.Setup(n => n.Confirm(It.IsAny<string>())).Returns(confirmed);
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.SelectedCustomer = CustomersTestContext.Ada;

        await viewModel.RemoveCommand.ExecuteAsync();

        FakeLogRecord[] records = this.context.RecordsOf<RemoveCustomerCommand>().ToArray();
        Assert.That(records.Select(r => r.Message), Has.Some.Contains(CustomersTestContext.Ada.Id.ToString()));
        IEnumerable<string?> values = records.SelectMany(r => r.StructuredState ?? Enumerable.Empty<KeyValuePair<string, string?>>()).Select(p => p.Value);
        string everything = string.Join("\n", records.Select(r => r.Message).Concat(values));
        Assert.That(everything, Does.Not.Contain(CustomersTestContext.Ada.Name));
        Assert.That(everything, Does.Not.Contain(CustomersTestContext.Ada.Email));
    }
}
