using System;
using System.Threading;
using LogDemo.App.NetFramework.Logging;
using LogDemo.App.NetFramework.Services;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.NetFramework.Commands;

/// <summary>
/// Exercises the global exception handlers. Parameter:
/// <list type="bullet">
/// <item><c>"UI"</c> - throws on the UI thread; the Dispatcher handler logs it, informs the user, and the app keeps running.</item>
/// <item><c>"Thread"</c> - throws on a worker thread; .NET terminates the process, but the log is flushed first.</item>
/// </list>
/// </summary>
public sealed class SimulateCrashCommand : DelegateBaseCommand<string>
{
    public const string UiThread = "UI";
    public const string WorkerThread = "Thread";

    private readonly IUserNotificationService notifications;

    public SimulateCrashCommand(IUserNotificationService notifications, ILogger<SimulateCrashCommand> logger)
        : base(logger)
    {
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    protected override bool CanInvoke(string parameter) => parameter is UiThread or WorkerThread;

    protected override void Invoke(string parameter)
    {
        if (parameter is WorkerThread)
        {
            if (!this.notifications.Confirm("This terminates the application (on purpose). Check the log afterwards.\n\nContinue?"))
            {
                return;
            }

            Logger.SimulatingCrash("worker thread");
            new Thread(() => throw new InvalidOperationException("Simulated crash on a worker thread"))
            {
                IsBackground = true,
                Name = "CrashDemo",
            }.Start();
            return;
        }

        Logger.SimulatingCrash("UI thread");
        throw new InvalidOperationException("Simulated unhandled exception on the UI thread");
    }
}
