using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Base class for asynchronous commands that take a <typeparamref name="T"/> parameter: disables itself
/// while running (no double-clicks), supports cancellation, and never lets an exception escape silently
/// from <c>async void</c>.
/// </summary>
public abstract class DelegateBaseAsyncCommand<T> : BaseCommand
{
    private CancellationTokenSource? cancellation;

    protected DelegateBaseAsyncCommand(ILogger logger)
        : base(logger)
    {
    }

    public bool IsExecuting { get; private set; }

    /// <summary>
    /// ICommand entry point. <c>async void</c> is unavoidable here; <see cref="ExecuteAsync"/> only
    /// throws for unhandled (already logged) failures, which then reach the Dispatcher's global handler.
    /// </summary>
    public sealed override async void Execute(object? parameter)
    {
        if (!TryGetParameter(parameter, out T value))
        {
            Logger.CommandSkipped(Name);
            return;
        }

        await ExecuteAsync(value);
    }

    /// <summary>Awaitable entry point, used by tests and by other code that needs to wait for completion.</summary>
    public async Task ExecuteAsync(T parameter)
    {
        if (!CanExecute(parameter))
        {
            Logger.CommandSkipped(Name);
            return;
        }

        using CancellationTokenSource cancellation = new CancellationTokenSource();
        this.cancellation = cancellation;
        IsExecuting = true;
        RaiseCanExecuteChanged();

        using (CommandExecution execution = BeginExecution())
        {
            Logger.CommandExecuting(Name);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await InvokeAsync(parameter, cancellation.Token);
                Logger.CommandCompleted(Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (HandleFailure(ex, stopwatch.ElapsedMilliseconds, execution))
            {
                // Logged and handled.
            }
            finally
            {
                this.cancellation = null;
                IsExecuting = false;
                RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Requests cancellation of the running execution, if any.</summary>
    public void Cancel() => this.cancellation?.Cancel();

    /// <summary>Additional conditions for starting; the "not already running" check is built in.</summary>
    protected virtual bool CanInvoke(T parameter) => true;

    protected abstract Task InvokeAsync(T parameter, CancellationToken cancellationToken);

    private protected sealed override bool CanExecuteParameter(object? parameter) =>
        !IsExecuting && TryGetParameter(parameter, out T value) && CanInvoke(value);
}

/// <summary>Base class for asynchronous commands that ignore their parameter.</summary>
public abstract class DelegateBaseAsyncCommand : DelegateBaseAsyncCommand<object?>
{
    protected DelegateBaseAsyncCommand(ILogger logger)
        : base(logger)
    {
    }
}
