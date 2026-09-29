using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LogDemo.App.Net.Logging;
using LogDemo.App.Net.Models;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.Net.Services;

/// <summary>Stands in for a web API / database: adds latency and can fail on demand.</summary>
public sealed class InMemoryCustomerService : ICustomerService
{
    private static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(600);

    private readonly object gate = new object();
    private readonly List<Customer> customers = new List<Customer>
    {
        new Customer(1001, "Ada Lovelace", "ada@example.com", "London"),
        new Customer(1002, "Grace Hopper", "grace@example.com", "New York"),
        new Customer(1003, "Alan Turing", "alan@example.com", "Manchester"),
        new Customer(1004, "Katherine Johnson", "katherine@example.com", "Hampton"),
        new Customer(1005, "Linus Torvalds", "linus@example.com", "Helsinki"),
        new Customer(1006, "Margaret Hamilton", "margaret@example.com", "Boston"),
    };

    private readonly DemoSettings settings;
    private readonly ILogger<InMemoryCustomerService> logger;

    public InMemoryCustomerService(DemoSettings settings, ILogger<InMemoryCustomerService> logger)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken)
    {
        await SimulateBackendCallAsync(nameof(GetCustomersAsync), cancellationToken).ConfigureAwait(false);

        lock (this.gate)
        {
            return this.customers.ToArray();
        }
    }

    public async Task RemoveCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        await SimulateBackendCallAsync(nameof(RemoveCustomerAsync), cancellationToken).ConfigureAwait(false);

        lock (this.gate)
        {
            this.customers.RemoveAll(c => c.Id == customerId);
        }
    }

    private async Task SimulateBackendCallAsync(string operation, CancellationToken cancellationToken)
    {
        this.logger.BackendCallStarted(operation);
        Stopwatch stopwatch = Stopwatch.StartNew();

        await Task.Delay(Latency, cancellationToken).ConfigureAwait(false);

        if (this.settings.SimulateBackendFailures)
        {
            this.logger.BackendCallFailedSimulated(operation);
            throw new CustomerServiceException(
                "The customer service did not respond.",
                new TimeoutException($"{operation} timed out after {Latency.TotalMilliseconds} ms (simulated)."));
        }

        this.logger.BackendCallCompleted(operation, stopwatch.ElapsedMilliseconds);
    }
}
