using System;
using System.Runtime.InteropServices;
using LogDemo.App.NetFramework.Commands;
using LogDemo.Logging;
using Prism.Mvvm;

namespace LogDemo.App.NetFramework.ViewModels;

public sealed class ShellViewModel : BindableBase
{
    public ShellViewModel(NavigateCommand navigateCommand, LogSession logSession)
    {
        NavigateCommand = navigateCommand ?? throw new ArgumentNullException(nameof(navigateCommand));
        LogFilePath = (logSession ?? throw new ArgumentNullException(nameof(logSession))).LogFilePath;
        Title = $"LogDemo ({RuntimeInformation.FrameworkDescription})";
    }

    public string Title { get; }

    public string LogFilePath { get; }

    public NavigateCommand NavigateCommand { get; }
}
