using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Base class for asynchronous commands: disables itself while running (no double-clicks),
/// supports cancellation, and never lets an exception escape silently from <c>async void</c>.
/// </summary>
public abstract class AsyncCommandBase : LoggingCommandBase
{
    private CancellationTokenSource? cancellation;

    protected AsyncCommandBase(ILogger logger)
        : base(logger)
    {
    }

    public bool IsExecuting { get; private set; }

    /// <summary>
    /// ICommand entry point. <c>async void</c> is unavoidable here; <see cref="ExecuteAsync"/> only
    /// throws for unhandled (already logged) failures, which then reach the Dispatcher's global handler.
    /// </summary>
    public sealed override async void Execute(object? parameter) => await ExecuteAsync(parameter);

    /// <summary>Awaitable entry point, used by tests and by other code that needs to wait for completion.</summary>
    public async Task ExecuteAsync(object? parameter)
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

        using (BeginExecutionScope())
        {
            Logger.CommandExecuting(Name);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await ExecuteCoreAsync(parameter, cancellation.Token);
                Logger.CommandCompleted(Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (HandleFailure(ex, stopwatch.ElapsedMilliseconds))
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

    protected sealed override bool CanExecuteCore(object? parameter) => !IsExecuting && CanStart(parameter);

    /// <summary>Additional conditions for starting; the "not already running" check is built in.</summary>
    protected virtual bool CanStart(object? parameter) => true;

    protected abstract Task ExecuteCoreAsync(object? parameter, CancellationToken cancellationToken);
}
