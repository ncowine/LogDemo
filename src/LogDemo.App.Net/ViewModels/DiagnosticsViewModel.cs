using System;
using LogDemo.App.Net.Commands;
using LogDemo.App.Net.Services;
using LogDemo.Logging;
using Prism.Mvvm;

namespace LogDemo.App.Net.ViewModels;

public sealed class DiagnosticsViewModel : BindableBase
{
    public DiagnosticsViewModel(
        LogSession session,
        FileLoggingOptions options,
        AppPaths paths,
        DemoSettings settings,
        OpenLogFolderCommand openLogFolderCommand,
        WriteSampleLogsCommand writeSampleLogsCommand,
        SimulateCrashCommand simulateCrashCommand)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        OpenLogFolderCommand = openLogFolderCommand ?? throw new ArgumentNullException(nameof(openLogFolderCommand));
        WriteSampleLogsCommand = writeSampleLogsCommand ?? throw new ArgumentNullException(nameof(writeSampleLogsCommand));
        SimulateCrashCommand = simulateCrashCommand ?? throw new ArgumentNullException(nameof(simulateCrashCommand));
    }

    public LogSession Session { get; }

    public FileLoggingOptions Options { get; }

    public AppPaths Paths { get; }

    public DemoSettings Settings { get; }

    public OpenLogFolderCommand OpenLogFolderCommand { get; }

    public WriteSampleLogsCommand WriteSampleLogsCommand { get; }

    public SimulateCrashCommand SimulateCrashCommand { get; }
}
