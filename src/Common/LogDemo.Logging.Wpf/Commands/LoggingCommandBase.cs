using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Shared plumbing for <see cref="CommandBase"/> and <see cref="AsyncCommandBase"/>: every execution
/// runs inside a logging scope (command name + short correlation id), is timed, and failures are
/// logged exactly once.
/// </summary>
public abstract class LoggingCommandBase : ICommand
{
    protected LoggingCommandBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>Name used in log messages. Defaults to the class name.</summary>
    public virtual string Name => GetType().Name;

    protected ILogger Logger { get; }

    public bool CanExecute(object? parameter)
    {
        try
        {
            return CanExecuteCore(parameter);
        }
        catch (Exception ex)
        {
            // CanExecute runs during layout; throwing here would take the UI down.
            Logger.CanExecuteFailed(Name, ex);
            return false;
        }
    }

    public abstract void Execute(object? parameter);

    /// <summary>Re-queries <see cref="CanExecute"/>. Safe to call from any thread.</summary>
    public void RaiseCanExecuteChanged()
    {
        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty)));
        }
    }

    protected virtual bool CanExecuteCore(object? parameter) => true;

    /// <summary>
    /// Override for <em>expected</em> failures the command can present itself (inline message, dialog).
    /// Return <c>true</c> when handled; the failure is then logged as a warning and not rethrown.
    /// Unexpected failures (return <c>false</c>) are logged as errors and rethrown to the global handler.
    /// </summary>
    protected virtual bool TryHandleFailure(Exception exception) => false;

    /// <summary>
    /// Everything logged while the command runs - including by services it calls, across awaits -
    /// carries <c>CommandName</c> and <c>CommandId</c>, so one user action can be followed through the log.
    /// </summary>
    private protected IDisposable? BeginExecutionScope() =>
        Logger.BeginScope("{CommandName}#{CommandId}", Name, Guid.NewGuid().ToString("N").Substring(0, 6));

    /// <summary>Logs the failure and returns whether it was handled (i.e. must not be rethrown).</summary>
    private protected bool HandleFailure(Exception exception, long elapsedMs)
    {
        if (exception is OperationCanceledException)
        {
            Logger.CommandCanceled(Name, elapsedMs);
            return true;
        }

        if (TryHandleFailure(exception))
        {
            Logger.CommandFailedHandled(Name, elapsedMs, exception);
            return true;
        }

        Logger.CommandFailed(Name, elapsedMs, exception);
        exception.MarkAsLogged();
        return false;
    }
}
