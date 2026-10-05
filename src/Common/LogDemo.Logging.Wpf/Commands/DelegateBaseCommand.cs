using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Base class for synchronous commands that take a <typeparamref name="T"/> parameter.
/// Derived classes implement <see cref="Invoke"/>.
/// </summary>
public abstract class DelegateBaseCommand<T> : BaseCommand
{
    protected DelegateBaseCommand(ILogger logger)
        : base(logger)
    {
    }

    public sealed override void Execute(object? parameter)
    {
        if (!CanExecute(parameter) || !TryGetParameter(parameter, out T value))
        {
            Logger.CommandSkipped(Name);
            return;
        }

        using (CommandExecution execution = BeginExecution())
        {
            Logger.CommandExecuting(Name);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                Invoke(value);
                Logger.CommandCompleted(Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (HandleFailure(ex, stopwatch.ElapsedMilliseconds, execution))
            {
                // Logged and handled. The filter runs before the stack unwinds, so the log entry
                // still has the command scope; unhandled exceptions propagate to the global handler.
            }
        }
    }

    protected virtual bool CanInvoke(T parameter) => true;

    protected abstract void Invoke(T parameter);

    private protected sealed override bool CanExecuteParameter(object? parameter) =>
        TryGetParameter(parameter, out T value) && CanInvoke(value);
}

/// <summary>Base class for synchronous commands that ignore their parameter.</summary>
public abstract class DelegateBaseCommand : DelegateBaseCommand<object?>
{
    protected DelegateBaseCommand(ILogger logger)
        : base(logger)
    {
    }
}
