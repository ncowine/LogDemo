using System;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;
using Prism.DryIoc;
using Prism.Ioc;

namespace LogDemo.Logging.Tests;

/// <summary>ContainerLocator is global: every test here sets it up and resets it, and no other test class touches it.</summary>
[NonParallelizable]
public sealed class CommandFactoryTests
{
    [SetUp]
    public void SetUp()
    {
        DryIocContainerExtension container = new DryIocContainerExtension();
        container.RegisterInstance<ILogger>(new FakeLogger());
        container.Register<OwnedCommand>();
        container.Register<StandaloneCommand>();
        ContainerLocator.SetContainerExtension(() => container);
    }

    [TearDown]
    public void TearDown() => ContainerLocator.ResetContainer();

    [Test]
    public void Create_with_view_model_passes_it_to_the_command()
    {
        OwnerViewModel owner = new OwnerViewModel();

        OwnedCommand command = CommandFactory.Create<OwnedCommand>(owner);

        Assert.That(command.Owner, Is.SameAs(owner));
    }

    [Test]
    public void Create_returns_a_new_command_on_every_call()
    {
        OwnerViewModel owner = new OwnerViewModel();

        OwnedCommand first = CommandFactory.Create<OwnedCommand>(owner);
        OwnedCommand second = CommandFactory.Create<OwnedCommand>(owner);

        Assert.That(second, Is.Not.SameAs(first));
    }

    [Test]
    public void Create_without_view_model_resolves_all_parameters_from_the_container()
    {
        StandaloneCommand command = CommandFactory.Create<StandaloneCommand>();

        Assert.That(command, Is.Not.Null);
    }

    [Test]
    public void Create_without_initialised_container_throws()
    {
        ContainerLocator.ResetContainer();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => CommandFactory.Create<StandaloneCommand>());

        Assert.That(thrown.Message, Does.Contain(nameof(StandaloneCommand)));
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
