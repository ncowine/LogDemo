using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>
/// Base class for synchronous commands that take a <typeparamref name="T"/> parameter.
/// Derived classes implement <see cref="Invoke"/>.
/// </summary>
public abstract class DelegateBaseCommand<T> : BaseCommand, IDelegateBaseCommand<T>
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

    // Explicit: as public members they would duplicate Execute(object?) / CanExecute(object?) when T is object?.
    void IDelegateBaseCommand<T>.Execute(T parameter) => Execute(parameter);

    bool IDelegateBaseCommand<T>.CanExecute(T parameter) => CanExecute(parameter);

    protected virtual bool CanInvoke(T parameter) => true;

    protected abstract void Invoke(T parameter);

    private protected sealed override bool CanExecuteParameter(object? parameter) =>
        TryGetParameter(parameter, out T value) && CanInvoke(value);
}

/// <summary>Base class for synchronous commands that ignore their parameter.</summary>
public abstract class DelegateBaseCommand : DelegateBaseCommand<object?>, IDelegateBaseCommand
{
    protected DelegateBaseCommand(ILogger logger)
        : base(logger)
    {
    }

    /// <summary>Runs the command from code; the parameter is ignored, so none has to be passed.</summary>
    public void Execute() => Execute(null);

    public bool CanExecute() => CanExecute(null);

    /// <summary>Additional conditions for running the command.</summary>
    protected virtual bool CanInvoke() => true;

    /// <summary>The command's work. Derived classes implement this instead of the parameterised overload.</summary>
    protected abstract void Invoke();

    // The parameter is ignored: seal the object? overloads and route them to the parameterless ones,
    // so derived classes only ever see (and are asked to implement) Invoke() / CanInvoke().
    protected sealed override bool CanInvoke(object? parameter) => CanInvoke();

    protected sealed override void Invoke(object? parameter) => Invoke();
}
