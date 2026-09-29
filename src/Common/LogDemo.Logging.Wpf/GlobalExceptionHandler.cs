using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace LogDemo.Logging.Wpf;

/// <summary>
/// Catches everything that would otherwise escape: UI thread, other threads, and unobserved tasks.
/// Install it right after the <see cref="LoggingHost"/> is created, before Prism builds the container.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>UI thread: log, tell the user, keep running, so the user can still save their work. An exception
/// during layout/rendering repeats every frame though, so after too many in a short time we shut down.</item>
/// <item>Other threads: the process is going down; log and flush synchronously.</item>
/// <item>Unobserved tasks: log and mark observed (usually a fire-and-forget bug, rarely fatal).</item>
/// </list>
/// </remarks>
public sealed class GlobalExceptionHandler : IDisposable
{
    private const int ErrorLoopThreshold = 5;
    private const string Caption = "Error";
    private static readonly TimeSpan ErrorLoopWindow = TimeSpan.FromSeconds(10);

    private readonly Application application;
    private readonly LoggingHost logging;
    private readonly ILogger logger;
    private readonly ConcurrentQueue<DateTime> recentUiErrors = new ConcurrentQueue<DateTime>();

    private GlobalExceptionHandler(Application application, LoggingHost logging)
    {
        this.application = application;
        this.logging = logging;
        this.logger = logging.LoggerFactory.CreateLogger<GlobalExceptionHandler>();

        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        application.SessionEnding += OnSessionEnding;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static GlobalExceptionHandler Install(Application application, LoggingHost logging)
    {
        if (application is null)
        {
            throw new ArgumentNullException(nameof(application));
        }

        if (logging is null)
        {
            throw new ArgumentNullException(nameof(logging));
        }

        return new GlobalExceptionHandler(application, logging);
    }

    /// <summary>
    /// Call from a <c>catch</c> around <c>base.OnStartup</c>. Without it the Dispatcher handler would
    /// swallow the error and leave a window-less process running forever.
    /// </summary>
    public void HandleStartupFailure(Exception exception)
    {
        this.logger.StartupFailed(exception);
        ShowError($"The application could not start.\n\n{exception.Message}\n\nDetails: {this.logging.Session.LogFilePath}");
        this.application.Shutdown(1);
    }

    public void Dispose()
    {
        this.application.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        this.application.SessionEnding -= OnSessionEnding;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Exception exception = e.Exception;
        if (exception.IsLogged())
        {
            this.logger.UnhandledUiExceptionAlreadyLogged(exception.GetType().FullName ?? exception.GetType().Name);
        }
        else
        {
            this.logger.UnhandledUiException(exception);
        }

        e.Handled = true;

        if (IsErrorLoop())
        {
            this.logger.ErrorLoopDetected(ErrorLoopThreshold, (int)ErrorLoopWindow.TotalSeconds);
            ShowError($"The application ran into repeated errors and will close.\n\nDetails: {this.logging.Session.LogFilePath}");
            this.application.Shutdown(2);
            return;
        }

        ShowError($"Something went wrong: {exception.Message}\n\nThe details were written to:\n{this.logging.Session.LogFilePath}");
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        this.logger.UnhandledDomainException(e.IsTerminating, e.ExceptionObject as Exception);
        if (e.IsTerminating)
        {
            this.logging.Dispose(); // flush before the runtime kills the process
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        this.logger.UnobservedTaskException(e.Exception);
        e.SetObserved();
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        this.logger.SessionEnding(e.ReasonSessionEnding.ToString());
    }

    private bool IsErrorLoop()
    {
        DateTime now = DateTime.UtcNow;
        this.recentUiErrors.Enqueue(now);
        while (this.recentUiErrors.TryPeek(out DateTime oldest) && now - oldest > ErrorLoopWindow)
        {
            this.recentUiErrors.TryDequeue(out _);
        }

        return this.recentUiErrors.Count >= ErrorLoopThreshold;
    }

    private void ShowError(string message)
    {
        try
        {
            Window? owner = this.application.MainWindow is { IsVisible: true } ? this.application.MainWindow : null;
            if (owner is null)
            {
                MessageBox.Show(message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(owner, message, Caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            // The dialog itself failed (e.g. during shutdown). Nothing left but the log.
            this.logger.ErrorDialogFailed(ex);
        }
    }
}
