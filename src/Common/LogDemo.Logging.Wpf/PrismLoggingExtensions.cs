using System;
using System.Collections.Generic;
using DryIoc;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Ioc;

namespace LogDemo.Logging.Wpf;

public static class PrismLoggingExtensions
{
    /// <summary>
    /// Makes <c>ILogger&lt;T&gt;</c> injectable everywhere (open generic over the host's factory) and
    /// registers <see cref="LogSession"/> and <see cref="FileLoggingOptions"/> for diagnostics screens.
    /// </summary>
    public static IContainerRegistry RegisterLogging(this IContainerRegistry containerRegistry, LoggingHost logging)
    {
        if (containerRegistry is null)
        {
            throw new ArgumentNullException(nameof(containerRegistry));
        }

        if (logging is null)
        {
            throw new ArgumentNullException(nameof(logging));
        }

        containerRegistry.RegisterInstance(logging.LoggerFactory);
        containerRegistry.RegisterInstance(logging.Session);
        containerRegistry.RegisterInstance(logging.Options);
        containerRegistry.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
        return containerRegistry;
    }

    /// <summary>
    /// Fails fast on a missing or broken registration at startup, instead of on the first click.
    /// Throws, so call it where startup failures are caught and logged.
    /// </summary>
    public static void ValidateRegistrations(this IContainerProvider containerProvider)
    {
        if (containerProvider is null)
        {
            throw new ArgumentNullException(nameof(containerProvider));
        }

        IContainer container = containerProvider.GetContainer();
        foreach (KeyValuePair<ServiceInfo, ContainerException> error in container.Validate())
        {
            throw new InvalidOperationException($"Container registration error for {error.Key}: {error.Value.Message}", error.Value);
        }
    }
}
