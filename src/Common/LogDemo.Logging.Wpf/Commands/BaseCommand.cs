using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Shared plumbing for <see cref="DelegateBaseCommand{T}"/> and <see cref="DelegateBaseAsyncCommand{T}"/>: every execution
/// runs inside a logging scope (command name + short correlation id) and a trace span, is timed, and
/// failures are logged exactly once.
/// </summary>
public abstract class BaseCommand : IBaseCommand
{
    private protected BaseCommand(ILogger logger)
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
            return CanExecuteParameter(parameter);
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

    /// <summary>Converts the untyped parameter and evaluates the derived command's conditions.</summary>
    private protected abstract bool CanExecuteParameter(object? parameter);

    /// <summary>
    /// Casts the ICommand parameter to <typeparamref name="T"/>. <c>null</c> is accepted when <typeparamref name="T"/>
    /// allows it (nullable annotations are not enforced, so a <c>string</c> command can still receive <c>null</c>).
    /// A parameter of the wrong type makes the command disabled rather than throwing during binding.
    /// </summary>
    private protected static bool TryGetParameter<T>(object? parameter, out T value)
    {
        if (parameter is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return parameter is null && default(T) is null;
    }

    /// <summary>
    /// Override for <em>expected</em> failures the command can present itself (inline message, dialog).
    /// Return <c>true</c> when handled; the failure is then logged as a warning and not rethrown.
    /// Unexpected failures (return <c>false</c>) are logged as errors and rethrown to the global handler.
    /// </summary>
    protected virtual bool TryHandleFailure(Exception exception) => false;

    /// <summary>
    /// Everything logged while the command runs - including by services it calls, across awaits -
    /// carries <c>CommandName</c> and <c>CommandId</c>, so one user action can be followed through the log.
    /// When central export is on, the execution is also a span: HTTP calls it makes become child spans,
    /// and its log records carry the trace id, which links Loki and Tempo both ways.
    /// </summary>
    private protected CommandExecution BeginExecution()
    {
        string commandId = Guid.NewGuid().ToString("N").Substring(0, 6);
        Activity? activity = AppTracing.Source.StartActivity(Name);
        activity?.SetTag("command.id", commandId);
        IDisposable? scope = Logger.BeginScope("{CommandName}#{CommandId}", Name, commandId);
        return new CommandExecution(scope, activity);
    }

    /// <summary>Logs the failure and returns whether it was handled (i.e. must not be rethrown).</summary>
    private protected bool HandleFailure(Exception exception, long elapsedMs, CommandExecution execution)
    {
        if (exception is OperationCanceledException)
        {
            Logger.CommandCanceled(Name, elapsedMs);
            return true;
        }

        execution.Failed(exception);

        if (TryHandleFailure(exception))
        {
            Logger.CommandFailedHandled(Name, elapsedMs, exception);
            return true;
        }

        Logger.CommandFailed(Name, elapsedMs, exception);
        exception.MarkAsLogged();
        return false;
    }

    /// <summary>The logging scope and span of one execution; ends both when disposed.</summary>
    private protected sealed class CommandExecution : IDisposable
    {
        private readonly IDisposable? scope;
        private readonly Activity? activity;

        public CommandExecution(IDisposable? scope, Activity? activity)
        {
            this.scope = scope;
            this.activity = activity;
        }

        /// <summary>Marks the span as failed, handled or not: in Tempo both are errors worth seeing.</summary>
        public void Failed(Exception exception)
        {
            this.activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            this.activity?.AddException(exception);
        }

        public void Dispose()
        {
            this.scope?.Dispose();
            this.activity?.Dispose();
        }
    }
}
