using System;
using System.Windows;

namespace LogDemo.App.Net.Services;

public sealed class MessageBoxNotificationService : IUserNotificationService
{
    private const string Caption = "LogDemo";

    public void ShowError(string message) =>
        OnUiThread(owner => ShowMessage(owner, message, MessageBoxButton.OK, MessageBoxImage.Error));

    public bool Confirm(string message) =>
        OnUiThread(owner => ShowMessage(owner, message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);

    private static MessageBoxResult ShowMessage(Window? owner, string message, MessageBoxButton buttons, MessageBoxImage image) =>
        owner is null
            ? MessageBox.Show(message, Caption, buttons, image)
            : MessageBox.Show(owner, message, Caption, buttons, image);

    private static T OnUiThread<T>(Func<Window?, T> show)
    {
        Application? app = Application.Current;
        if (app is null || app.Dispatcher.CheckAccess())
        {
            return show(GetOwner(app));
        }

        return app.Dispatcher.Invoke(() => show(GetOwner(app)));
    }

    private static Window? GetOwner(Application? app)
    {
        Window? owner = app?.MainWindow;
        return owner is not null && owner.IsVisible ? owner : null;
    }
}
