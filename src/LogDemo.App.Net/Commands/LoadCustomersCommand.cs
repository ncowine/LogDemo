using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDemo.App.Net.Logging;
using LogDemo.App.Net.Models;
using LogDemo.App.Net.Services;
using LogDemo.App.Net.ViewModels;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.Net.Commands;

public sealed class LoadCustomersCommand : DelegateBaseAsyncCommand
{
    internal const string LoadFailedMessage =
        "Customers could not be loaded. Please try again in a moment. (The details were written to the log file.)";

    private readonly CustomersViewModel viewModel;
    private readonly ICustomerService customerService;

    public LoadCustomersCommand(CustomersViewModel viewModel, ICustomerService customerService, ILogger<LoadCustomersCommand> logger)
        : base(logger)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
    }

    protected override bool CanInvoke(object? parameter) => !this.viewModel.IsBusy;

    protected override async Task InvokeAsync(object? parameter, CancellationToken cancellationToken)
    {
        this.viewModel.IsBusy = true;
        this.viewModel.ErrorMessage = null;
        try
        {
            IReadOnlyList<Customer> customers = await this.customerService.GetCustomersAsync(cancellationToken);
            this.viewModel.ReplaceCustomers(customers);
            Logger.CustomersLoaded(customers.Count);
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
            this.viewModel.ErrorMessage = LoadFailedMessage;
            return true;
        }

        return false;
    }
}
