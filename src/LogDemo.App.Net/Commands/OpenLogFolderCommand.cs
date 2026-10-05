using System;
using System.ComponentModel;
using LogDemo.App.Net.Logging;
using LogDemo.App.Net.Services;
using LogDemo.Logging;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;

namespace LogDemo.App.Net.Commands;

/// <summary>Opens Explorer on the current log file - the first thing support asks for.</summary>
public sealed class OpenLogFolderCommand : DelegateBaseCommand
{
    private readonly LogSession session;
    private readonly IShellLauncher shell;
    private readonly IUserNotificationService notifications;

    public OpenLogFolderCommand(
        LogSession session,
        IShellLauncher shell,
        IUserNotificationService notifications,
        ILogger<OpenLogFolderCommand> logger)
        : base(logger)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    protected override void Invoke(object? parameter)
    {
        this.shell.RevealFile(this.session.LogFilePath);
        Logger.LogFolderOpened(this.session.LogDirectory);
    }

    protected override bool TryHandleFailure(Exception exception)
    {
        if (exception is Win32Exception)
        {
            this.notifications.ShowError($"Explorer could not be started. The logs are in:\n{this.session.LogDirectory}");
            return true;
        }

        return false;
    }
}
