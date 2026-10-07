using System;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Prism.DryIoc;
using Prism.Ioc;
using Xunit;

namespace LogDemo.Logging.Tests;

// ContainerLocator is global: every test here sets it up and resets it, and no other test class touches it.
public sealed class CommandFactoryTests : IDisposable
{
    public CommandFactoryTests()
    {
        DryIocContainerExtension container = new DryIocContainerExtension();
        container.RegisterInstance<ILogger>(new FakeLogger());
        container.Register<OwnedCommand>();
        container.Register<StandaloneCommand>();
        ContainerLocator.SetContainerExtension(() => container);
    }

    public void Dispose() => ContainerLocator.ResetContainer();

    [Fact]
    public void Create_with_view_model_passes_it_to_the_command()
    {
        OwnerViewModel owner = new OwnerViewModel();

        OwnedCommand command = CommandFactory.Create<OwnedCommand>(owner);

        Assert.Same(owner, command.Owner);
    }

    [Fact]
    public void Create_returns_a_new_command_on_every_call()
    {
        OwnerViewModel owner = new OwnerViewModel();

        Assert.NotSame(CommandFactory.Create<OwnedCommand>(owner), CommandFactory.Create<OwnedCommand>(owner));
    }

    [Fact]
    public void Create_without_view_model_resolves_all_parameters_from_the_container()
    {
        StandaloneCommand command = CommandFactory.Create<StandaloneCommand>();

        Assert.NotNull(command);
    }

    [Fact]
    public void Create_without_initialised_container_throws()
    {
        ContainerLocator.ResetContainer();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => CommandFactory.Create<StandaloneCommand>());

        Assert.Contains(nameof(StandaloneCommand), thrown.Message);
    }

    private sealed class OwnerViewModel
    {
    }

    private sealed class OwnedCommand : DelegateBaseCommand
    {
        public OwnedCommand(OwnerViewModel owner, ILogger logger)
            : base(logger)
        {
            Owner = owner;
        }

        public OwnerViewModel Owner { get; }

        protected override void Invoke()
        {
        }
    }

    private sealed class StandaloneCommand : DelegateBaseCommand
    {
        public StandaloneCommand(ILogger logger)
            : base(logger)
        {
        }

        protected override void Invoke()
        {
        }
    }
}
