using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDemo.App.NetFramework.Models;

namespace LogDemo.App.NetFramework.Services;

public interface ICustomerService
{
    /// <exception cref="CustomerServiceException">The backend is unavailable.</exception>
    Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken cancellationToken);

    /// <exception cref="CustomerServiceException">The backend is unavailable.</exception>
    Task RemoveCustomerAsync(int customerId, CancellationToken cancellationToken);
}

/// <summary>An expected, recoverable backend failure (as opposed to a bug).</summary>
public sealed class CustomerServiceException : Exception
{
    public CustomerServiceException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
