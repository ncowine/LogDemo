using System;

namespace LogDemo.Logging;

/// <summary>
/// Marks exceptions that were already logged with full context (e.g. by a command) so the global
/// handler doesn't write the same stack trace twice.
/// </summary>
public static class ExceptionLoggingExtensions
{
    private const string LoggedKey = "LogDemo.AlreadyLogged";

    public static void MarkAsLogged(this Exception exception)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        try
        {
            exception.Data[LoggedKey] = true;
        }
        catch (Exception ex) when (ex is NotSupportedException || ex is ArgumentException)
        {
            // Data can be read-only for some exception types; worst case the error is logged twice.
        }
    }

    public static bool IsLogged(this Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Data.Contains(LoggedKey))
            {
                return true;
            }
        }

        return false;
    }
}
