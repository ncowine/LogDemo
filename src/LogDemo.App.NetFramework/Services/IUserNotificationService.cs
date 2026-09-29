namespace LogDemo.App.NetFramework.Services;

/// <summary>Keeps MessageBox out of commands and view models so they stay unit-testable.</summary>
public interface IUserNotificationService
{
    void ShowError(string message);

    bool Confirm(string message);
}
