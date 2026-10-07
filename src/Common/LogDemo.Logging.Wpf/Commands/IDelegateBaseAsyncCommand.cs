using System.Threading.Tasks;

namespace LogDemo.Logging.Wpf.Commands;

/// <summary>Public surface of <see cref="DelegateBaseAsyncCommand{T}"/>: an asynchronous command with a typed parameter.</summary>
public interface IDelegateBaseAsyncCommand<in T> : IBaseCommand
{
    bool IsExecuting { get; }

    Task ExecuteAsync(T parameter);

    bool CanExecute(T parameter);

    /// <summary>Requests cancellation of the running execution, if any.</summary>
    void Cancel();
}

/// <summary>
/// Public surface of <see cref="DelegateBaseAsyncCommand"/>: an asynchronous command without a parameter.
/// Deliberately not derived from <see cref="IDelegateBaseAsyncCommand{T}"/>, which would expose a parameter it ignores.
/// </summary>
public interface IDelegateBaseAsyncCommand : IBaseCommand
{
    bool IsExecuting { get; }

    Task ExecuteAsync();

    bool CanExecute();

    /// <summary>Requests cancellation of the running execution, if any.</summary>
    void Cancel();
}
