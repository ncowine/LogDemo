using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Prism.DryIoc;
using Prism.Ioc;

namespace LogDemo.App.Tests;

/// <summary>
/// What <see cref="CustomersViewModel"/> needs to be created: it builds its commands through
/// <c>CommandFactory</c>, so Prism's global <see cref="ContainerLocator"/> points at a container with mocked
/// services until <see cref="Dispose"/>. Everything logged lands in <see cref="Logs"/>.
/// </summary>
internal sealed class CustomersTestContext : IDisposable
{
    public static readonly Customer Ada = new Customer(1001, "Ada Lovelace", "ada@example.com", "London");
    public static readonly Customer Grace = new Customer(1002, "Grace Hopper", "grace@example.com", "New York");

    private readonly ILoggerFactory loggerFactory;

    public CustomersTestContext()
    {
        FakeLogCollector logs = new FakeLogCollector();
        Logs = logs;
        this.loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new FakeLoggerProvider(logs)));

        DryIocContainerExtension container = new DryIocContainerExtension();
        container.RegisterInstance(this.loggerFactory);
        container.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
        container.RegisterInstance(CustomerService.Object);
        container.RegisterInstance(Notifications.Object);
        container.Register<LoadCustomersCommand>();
        container.Register<RemoveCustomerCommand>();
        ContainerLocator.SetContainerExtension(() => container);
    }

    public Mock<ICustomerService> CustomerService { get; } = new Mock<ICustomerService>();

    public Mock<IUserNotificationService> Notifications { get; } = new Mock<IUserNotificationService>();

    public FakeLogCollector Logs { get; }

    public CustomersViewModel CreateViewModel() =>
        new CustomersViewModel(this.loggerFactory.CreateLogger<CustomersViewModel>());

    /// <summary>A view model already showing <see cref="Ada"/> and <see cref="Grace"/>.</summary>
    public CustomersViewModel CreateLoadedViewModel()
    {
        CustomersViewModel viewModel = CreateViewModel();
        viewModel.ReplaceCustomers(new[] { Ada, Grace });
        return viewModel;
    }

    /// <summary>Records written by loggers of <typeparamref name="T"/>.</summary>
    public IReadOnlyList<FakeLogRecord> RecordsOf<T>() =>
        Logs.GetSnapshot().Where(r => r.Category == typeof(T).FullName).ToList();

    public void Dispose()
    {
        ContainerLocator.ResetContainer();
        this.loggerFactory.Dispose();
    }
}
