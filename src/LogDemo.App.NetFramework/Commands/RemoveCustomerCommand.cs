using System;
using System.Threading;
using System.Threading.Tasks;
using LogDemo.App.NetFramework.Logging;
using LogDemo.App.NetFramework.Models;
using LogDemo.App.NetFramework.Services;
using LogDemo.App.NetFramework.ViewModels;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.NetFramework.Commands;

public sealed class RemoveCustomerCommand : DelegateBaseAsyncCommand
{
    private readonly CustomersViewModel viewModel;
    private readonly ICustomerService customerService;
    private readonly IUserNotificationService notifications;

    public RemoveCustomerCommand(
        CustomersViewModel viewModel,
        ICustomerService customerService,
        IUserNotificationService notifications,
        ILogger<RemoveCustomerCommand> logger)
        : base(logger)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    protected override bool CanInvoke(object? parameter) => this.viewModel.SelectedCustomer is not null && !this.viewModel.IsBusy;

    protected override async Task InvokeAsync(object? parameter, CancellationToken cancellationToken)
    {
        Customer customer = this.viewModel.SelectedCustomer!;

        // The name is shown to the user but only the id is logged (no personal data in log files).
        if (!this.notifications.Confirm($"Remove customer '{customer.Name}'?"))
        {
            Logger.CustomerRemovalDeclined(customer.Id);
            return;
        }

        this.viewModel.IsBusy = true;
        try
        {
            await this.customerService.RemoveCustomerAsync(customer.Id, cancellationToken);
            this.viewModel.Customers.Remove(customer);
            Logger.CustomerRemoved(customer.Id);
        }
        finally
        {
            this.viewModel.IsBusy = false;
        }
    }

    protected override bool TryHandleFailure(Exception exception)
    {
        if (exception is CustomerServiceException)
        {
            this.notifications.ShowError("The customer could not be removed. Please try again later.");
            return true;
        }

        return false;
    }
}
