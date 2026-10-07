using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Prism.Regions;

namespace LogDemo.App.Tests;

[NonParallelizable] // CustomersTestContext sets Prism's global ContainerLocator
public sealed class CustomersViewModelTests
{
    private CustomersTestContext context = null!;

    [SetUp]
    public void SetUp() => this.context = new CustomersTestContext();

    [TearDown]
    public void TearDown() => this.context.Dispose();

    [Test]
    public void Commands_are_created_for_this_view_model()
    {
        CustomersViewModel first = this.context.CreateViewModel();
        CustomersViewModel second = this.context.CreateViewModel();

        Assert.That(second.LoadCommand, Is.Not.SameAs(first.LoadCommand));
        Assert.That(second.RemoveCommand, Is.Not.SameAs(first.RemoveCommand));
    }

    [Test]
    public void First_navigation_loads_the_customers()
    {
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CustomersTestContext.Ada, CustomersTestContext.Grace });
        CustomersViewModel viewModel = this.context.CreateViewModel();

        ((INavigationAware)viewModel).OnNavigatedTo(null!);

        Assert.That(viewModel.Customers, Is.EqualTo(new[] { CustomersTestContext.Ada, CustomersTestContext.Grace }));
        Assert.That(viewModel.LastRefreshed, Is.Not.Null);
    }

    [Test]
    public void Navigating_back_does_not_reload()
    {
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CustomersTestContext.Ada });
        CustomersViewModel viewModel = this.context.CreateViewModel();
        INavigationAware navigation = viewModel;

        navigation.OnNavigatedTo(null!);
        navigation.OnNavigatedFrom(null!);
        navigation.OnNavigatedTo(null!);

        this.context.CustomerService.Verify(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Navigating_away_cancels_a_running_load()
    {
        CancellationToken received = default;
        TaskCompletionSource<IReadOnlyList<Customer>> pending = new TaskCompletionSource<IReadOnlyList<Customer>>();
        this.context.CustomerService
            .Setup(s => s.GetCustomersAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) =>
            {
                received = token;
                token.Register(() => pending.TrySetCanceled(token));
                return pending.Task;
            });
        CustomersViewModel viewModel = this.context.CreateViewModel();
        INavigationAware navigation = viewModel;

        navigation.OnNavigatedTo(null!);
        Assert.That(viewModel.IsBusy, Is.True);

        navigation.OnNavigatedFrom(null!);

        Assert.That(received.IsCancellationRequested, Is.True);
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.Customers, Is.Empty);
        Assert.That(viewModel.ErrorMessage, Is.Null); // cancelling is not an error the user needs to see
    }

    [Test]
    public void Selecting_a_customer_requeries_the_remove_command()
    {
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        int raised = 0;
        viewModel.RemoveCommand.CanExecuteChanged += (_, _) => raised++;

        viewModel.SelectedCustomer = CustomersTestContext.Ada;

        Assert.That(raised, Is.EqualTo(1));
        Assert.That(viewModel.RemoveCommand.CanExecute(), Is.True);
    }

    [Test]
    public void Busy_state_requeries_both_commands()
    {
        CustomersViewModel viewModel = this.context.CreateLoadedViewModel();
        viewModel.SelectedCustomer = CustomersTestContext.Ada;
        int loadRaised = 0;
        int removeRaised = 0;
        viewModel.LoadCommand.CanExecuteChanged += (_, _) => loadRaised++;
        viewModel.RemoveCommand.CanExecuteChanged += (_, _) => removeRaised++;

        viewModel.IsBusy = true;

        Assert.That(loadRaised, Is.EqualTo(1));
        Assert.That(removeRaised, Is.EqualTo(1));
        Assert.That(viewModel.LoadCommand.CanExecute(), Is.False);
        Assert.That(viewModel.RemoveCommand.CanExecute(), Is.False);
    }
}
