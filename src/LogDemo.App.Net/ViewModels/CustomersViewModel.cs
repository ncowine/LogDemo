using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using LogDemo.App.Net.Commands;
using LogDemo.App.Net.Logging;
using LogDemo.App.Net.Models;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;
using Prism.Regions;

namespace LogDemo.App.Net.ViewModels;

/// <summary>
/// Holds state only. Behaviour lives in the command classes, created through <see cref="CommandFactory"/> -
/// this view model is passed in, every other dependency (services, <c>ILogger&lt;TCommand&gt;</c>) is resolved
/// by the container.
/// </summary>
public sealed class CustomersViewModel : BindableBase, INavigationAware
{
    private readonly ILogger<CustomersViewModel> logger;
    private Customer? selectedCustomer;
    private bool isBusy;
    private string? errorMessage;
    private DateTime? lastRefreshed;

    public CustomersViewModel(ILogger<CustomersViewModel> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        LoadCommand = CommandFactory.Create<LoadCustomersCommand>(this);
        RemoveCommand = CommandFactory.Create<RemoveCustomerCommand>(this);
    }

    public LoadCustomersCommand LoadCommand { get; }

    public RemoveCustomerCommand RemoveCommand { get; }

    public ObservableCollection<Customer> Customers { get; } = new ObservableCollection<Customer>();

    public Customer? SelectedCustomer
    {
        get => this.selectedCustomer;
        set
        {
            if (SetProperty(ref this.selectedCustomer, value))
            {
                RemoveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => this.isBusy;
        internal set
        {
            if (SetProperty(ref this.isBusy, value))
            {
                LoadCommand.RaiseCanExecuteChanged();
                RemoveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>User-facing message; technical details go to the log, not the UI.</summary>
    public string? ErrorMessage
    {
        get => this.errorMessage;
        internal set => SetProperty(ref this.errorMessage, value);
    }

    public DateTime? LastRefreshed
    {
        get => this.lastRefreshed;
        private set => SetProperty(ref this.lastRefreshed, value);
    }

    internal void ReplaceCustomers(IEnumerable<Customer> customers)
    {
        Customers.Clear();
        foreach (Customer customer in customers)
        {
            Customers.Add(customer);
        }

        LastRefreshed = DateTime.Now;
    }

    // Prism calls this synchronously, so async void is the boundary; unhandled failures reach the Dispatcher handler.
    async void INavigationAware.OnNavigatedTo(NavigationContext navigationContext)
    {
        this.logger.ViewActivated(nameof(CustomersViewModel));
        if (LastRefreshed is null)
        {
            await LoadCommand.ExecuteAsync();
        }
    }

    bool INavigationAware.IsNavigationTarget(NavigationContext navigationContext) => true;

    void INavigationAware.OnNavigatedFrom(NavigationContext navigationContext)
    {
        this.logger.ViewDeactivated(nameof(CustomersViewModel));
        LoadCommand.Cancel(); // don't keep working for a page nobody is looking at
    }
}
