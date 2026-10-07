using System;
using Prism.Ioc;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Creates commands from Prism's container, optionally bound to the view model that owns them.
/// Replaces DryIoc's <c>Func&lt;TViewModel, TCommand&gt;</c> wrappers, so the view model needs no factory in its
/// constructor, and works with any Prism container. Register commands transient so each owner gets its own.
/// </summary>
/// <remarks>
/// Restricted to commands (<see cref="IBaseCommand"/>) on purpose: this is a command factory, not a general
/// service locator. Services still come in through constructors. Uses the global <see cref="ContainerLocator"/>:
/// tests point it at a test container with <see cref="ContainerLocator.SetContainerExtension"/> and call
/// <see cref="ContainerLocator.ResetContainer"/> afterwards.
/// </remarks>
public static class CommandFactory
{
    /// <summary>Resolves a new <typeparamref name="TCommand"/>; all constructor parameters come from the container.</summary>
    /// <exception cref="InvalidOperationException">The Prism container has not been initialised.</exception>
    public static TCommand Create<TCommand>()
        where TCommand : class, IBaseCommand
    {
        return (TCommand)GetContainer<TCommand>().Resolve(typeof(TCommand));
    }

    /// <summary>
    /// Resolves a new <typeparamref name="TCommand"/>, passing <paramref name="viewModel"/> to the constructor
    /// parameter of its type and resolving all other parameters normally.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Prism container has not been initialised.</exception>
    public static TCommand Create<TCommand>(object viewModel)
        where TCommand : class, IBaseCommand
    {
        if (viewModel is null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        return (TCommand)GetContainer<TCommand>().Resolve(typeof(TCommand), (viewModel.GetType(), viewModel));
    }

    // Read at call time, not cached in a static field, so tests can swap the container.
    private static IContainerProvider GetContainer<TCommand>() =>
        ContainerLocator.Container
            ?? throw new InvalidOperationException(
                $"Prism's container is not initialised; cannot create {typeof(TCommand).Name}.");
}
