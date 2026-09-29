using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>Base class for synchronous commands. Derived classes implement <see cref="ExecuteCore"/>.</summary>
public abstract class CommandBase : LoggingCommandBase
{
    protected CommandBase(ILogger logger)
        : base(logger)
    {
    }

    public sealed override void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            Logger.CommandSkipped(Name);
            return;
        }

        using (BeginExecutionScope())
        {
            Logger.CommandExecuting(Name);
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ExecuteCore(parameter);
                Logger.CommandCompleted(Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (HandleFailure(ex, stopwatch.ElapsedMilliseconds))
            {
                // Logged and handled. The filter runs before the stack unwinds, so the log entry
                // still has the command scope; unhandled exceptions propagate to the global handler.
            }
        }
    }

    protected abstract void ExecuteCore(object? parameter);
}
