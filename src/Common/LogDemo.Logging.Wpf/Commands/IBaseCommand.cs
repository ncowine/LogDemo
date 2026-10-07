using System.Windows.Input;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Public surface shared by every <see cref="BaseCommand"/>. Depend on this (or a derived interface)
/// instead of a concrete command where the command should be replaceable, e.g. by a mock in tests.
/// </summary>
public interface IBaseCommand : ICommand
{
    /// <summary>Name used in log messages.</summary>
    string Name { get; }

    /// <summary>Re-queries <see cref="ICommand.CanExecute"/>. Safe to call from any thread.</summary>
    void RaiseCanExecuteChanged();
}
