namespace LogDemo.Logging.Wpf.Commands;

/// <summary>Public surface of <see cref="DelegateBaseCommand{T}"/>: a synchronous command with a typed parameter.</summary>
public interface IDelegateBaseCommand<in T> : IBaseCommand
{
    void Execute(T parameter);

    bool CanExecute(T parameter);
}

/// <summary>Public surface of <see cref="DelegateBaseCommand"/>: a synchronous command without a parameter.</summary>
public interface IDelegateBaseCommand : IBaseCommand
{
    void Execute();

    bool CanExecute();
}
