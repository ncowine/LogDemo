# LogDemo: production-style logging for Prism 8 + DryIoc WPF apps

A deliberately small but realistic sample of `Microsoft.Extensions.Logging.ILogger<T>` in MVVM WPF apps
on **.NET Framework 4.7.2**, **.NET 8** and **.NET 10**, all built from the same sources.

- one log file per app start, e.g. `LogDemo-Net_20260929_101500_4242.log`
- configurable retention (default 7 days), with a max file count and a max file size
- log levels can change **while the app runs** (edit `appsettings.json`, no restart)
- every command execution gets a correlation scope, including the service calls it makes and code after `await`s
- global exception handling that logs once, keeps the UI alive when it can, and flushes the log before a crash

## Solution layout

```
src/
  Common/                     shared by every app, the only code the apps have in common
    LogDemo.Logging/            netstandard2.0   UI-agnostic logging core
      LoggingHost.cs              builds the ILoggerFactory (MEL + Serilog file sink), owns its lifetime
      AppConfigurationLoader.cs   appsettings.json + per-user file + env vars, survives malformed files
      AppPaths.cs                 exe folder, per-user data folder, default log folder
      FileLoggingOptions.cs       "FileLogging" config section, clamps invalid values instead of throwing
      LogSession.cs               the current run: session id, file path
      LogRetentionCleaner.cs      deletes expired sessions, prefix-scoped, never the current one
      LoggingLog.cs               the core's [LoggerMessage] definitions
    LogDemo.Logging.Wpf/        net472; net8.0-windows; net10.0-windows
      GlobalExceptionHandler.cs   Dispatcher / AppDomain / TaskScheduler handlers, startup failures
      PrismLoggingExtensions.cs   RegisterLogging (ILogger<T> in DryIoc), container validation
      Commands/                   LoggingCommandBase, CommandBase, AsyncCommandBase
      WpfLog.cs                   the WPF layer's [LoggerMessage] definitions
  LogDemo.App.NetFramework/   net472 exe
  LogDemo.App.Net/            net8.0-windows; net10.0-windows exe
                              each app: App.xaml.cs (startup order, DI registrations), Commands/,
                              ViewModels/, Views/, Services/, Logging/AppLog.cs, appsettings.json
tests/
  LogDemo.Logging.Tests/      xUnit for the Common projects, runs on net472, net8 and net10
```

The two apps deliberately share nothing but `src/Common`: each one owns its views, view models,
commands and services, the way two separate products would. None of the logging code differs between
.NET Framework and .NET, which is the main takeaway: `Microsoft.Extensions.Logging` and Serilog support
`net462+`, so the old-framework app doesn't need a separate logging approach.

## Running

```powershell
dotnet build LogDemo.sln
dotnet run --project src/LogDemo.App.NetFramework
dotnet run --project src/LogDemo.App.Net -f net8.0-windows
dotnet run --project src/LogDemo.App.Net -f net10.0-windows
dotnet test
```

Logs go to `%LOCALAPPDATA%\Contoso\LogDemo\Logs`. The **Diagnostics** page shows the current file, has an
*Open log folder* button, and lets you trigger handled errors, unhandled UI exceptions and a
worker-thread crash.

## Configuration

`appsettings.json` (next to the exe), then `%LOCALAPPDATA%\Contoso\LogDemo\appsettings.user.json`
(optional per-user overrides, handy when support needs Debug logs from one user), then environment
variables with the `LOGDEMO_` prefix. Later sources win.

```jsonc
{
  "Logging": {                       // standard MEL filters, hot-reloaded
    "LogLevel": {
      "Default": "Information",
      "LogDemo.App.Net.Commands": "Information",   // most specific category prefix wins
      "Microsoft": "Warning"
    }
  },
  "FileLogging": {                   // read at startup
    "Directory": "%LOCALAPPDATA%\\Contoso\\LogDemo\\Logs",   // env vars expanded; relative = exe folder
    "FilePrefix": null,              // default: the app name; retention only touches this prefix
    "RetentionDays": 7,              // 1..365
    "MaxRetainedFiles": 100,         // guards against crash loops; 0 = unlimited
    "MaxFileSizeMB": 20              // a session rolls to _001, _002... after this size
  }
}
```

Example: `set LOGDEMO_Logging__LogLevel__Default=Debug` before starting the app.

A malformed settings file never crashes the app. It is ignored and a warning is logged, both at startup
and when the file is edited while the app runs.

## Sample log

```
2026-09-29 21:53:07.833 +01:00 [INF] (T1) LogDemo.App.Net.App: ==== LogDemo-Net 1.0.0 starting | .NET 8.0.27 X64 | Microsoft Windows 10.0.19045 | pid 3464 | session 93f5bb69
2026-09-29 21:53:07.861 +01:00 [INF] (T5) LogDemo.Logging.LogRetentionCleaner: Log retention in C:\...\Logs (keep 7 days): scanned 3, deleted 0, failed 0
2026-09-29 21:53:14.335 +01:00 [WRN] (T5) LogDemo.App.Net.Services.InMemoryCustomerService: Backend call GetCustomersAsync failed (simulated) ["LoadCustomersCommand#df77cf"]
2026-09-29 21:53:14.360 +01:00 [WRN] (T1) LogDemo.App.Net.Commands.LoadCustomersCommand: LoadCustomersCommand failed after 631 ms, the user was informed ["LoadCustomersCommand#df77cf"]
LogDemo.App.Net.Services.CustomerServiceException: The customer service did not respond.
 ---> System.TimeoutException: GetCustomersAsync timed out after 600 ms (simulated).
2026-09-29 21:58:26.028 +01:00 [FTL] (T7) LogDemo.App.NetFramework.App: Unhandled exception (terminating: true) ["SimulateCrashCommand#fb6f00"]
```

The `["...#id"]` at the end is the logging scope. Service calls made by a command, including the ones
after `await`s on thread-pool threads, carry the command's id.

## Design decisions

**Only `ILogger<T>` in app code.** View models, commands and services depend on
`Microsoft.Extensions.Logging.ILogger<T>`, registered in DryIoc as an open generic
(`RegisterSingleton(typeof(ILogger<>), typeof(Logger<>))`). Serilog is an implementation detail inside
`LoggingHost`, and you can swap it for another provider without touching any view model.

**Logging exists before the container.** `App.OnStartup` builds configuration and logging
first, then calls Prism. That way container registration and shell creation are logged, along with any
startup failure. A startup failure is logged as critical, shown to the user, and the app exits with code
1. Without this, the Dispatcher handler would swallow the error and leave a process running with no window.

**One file per session, retention on startup.** The name is `{prefix}_{yyyyMMdd_HHmmss}_{pid}.log`.
Including the pid means two instances never share a file, so the faster non-shared sink can be used.
Retention runs on a background thread at startup, only matches `{prefix}_*.log`, never touches the
current session, and skips files that are still locked (it retries on the next start).

**Unbuffered file writes.** Each entry reaches the OS right away, so nothing is lost when the process is
killed. A desktop app logs too little for the cost to matter. Don't add `Serilog.Sinks.Async` unless you
measure a problem, because it trades crash safety for throughput.

**`[LoggerMessage]` source generation** (`AppLog.cs`, `LoggingLog.cs`, `WpfLog.cs`). You get stable event ids,
consistent wording in one place, and no allocations when a level is disabled. This works on net472 too.
`WriteSampleLogsCommand` uses plain `LogInformation(...)` templates for comparison.

**Commands as classes.** `LoggingCommandBase` wraps every execution in a scope, times it, and routes
failures through one path:

| Outcome | Logged as | Then |
|---|---|---|
| success | Debug (`completed in N ms`) | done |
| `OperationCanceledException` | Information | swallowed |
| expected failure (`TryHandleFailure` returns `true`) | Warning + exception | command informs the user (inline or dialog) |
| unexpected failure | Error + exception, marked as logged | rethrown to the global handler, which shows a dialog but doesn't log the stack trace again |

`AsyncCommandBase` also blocks re-entry while running, supports `Cancel()` (called when you navigate away
from Customers), and exposes an awaitable `ExecuteAsync` for tests.

Commands that need their view model get it through DryIoc's built-in **`Func<TViewModel, TCommand>`**
factory. The view model is passed in and everything else (services, `ILogger<TCommand>`) comes from the
container:

```csharp
public CustomersViewModel(
    Func<CustomersViewModel, LoadCustomersCommand> loadCommandFactory, ...)
{
    LoadCommand = loadCommandFactory(this);
}
```

**Global exception handlers**

| Source | Handling |
|---|---|
| `DispatcherUnhandledException` (UI thread) | log, dialog, keep running. After 5 in 10 s (e.g. an exception during layout that repeats every frame) log critical and shut down |
| `AppDomain.UnhandledException` (other threads) | log critical, then **dispose the logging host synchronously** so the file is flushed before the runtime kills the process |
| `TaskScheduler.UnobservedTaskException` | log error, mark as observed |

**No personal data in logs.** Customers are logged by id only, never by name or e-mail (see
`RemoveCustomerCommand`, and the test that checks it).

**Container validation at startup.** `OnInitialized` runs DryIoc's `Validate()`, so a missing registration
fails at startup with a clear log entry instead of when a user first clicks something.

## Pitfalls found while building this (with regression tests)

1. **`builder.AddSerilog()` overrides `Logging:LogLevel`.** The extension adds a `Trace` filter rule for
   the Serilog provider, and provider-specific rules beat the configured category rules, so the
   configured levels were ignored. The sample uses `builder.AddProvider(new SerilogLoggerProvider(...))`
   instead. Covered by `LoggingHostTests.Honours_Logging_LogLevel_configuration`.
2. **`LoggerFactory` doesn't dispose provider instances.** Providers passed as instances to
   `AddProvider` are never disposed, so the log file stayed open and wasn't flushed on exit.
   `LoggingHost` owns the Serilog logger and disposes it explicitly.
3. **Config hot reload can crash the process.** If `appsettings.json` is saved with a syntax error, the
   reload throws on a thread-pool thread. `SetFileLoadExceptionHandler` makes it ignore the bad file and
   log a warning instead.

## Where to go next

- Add machine-readable output with `Serilog.Formatting.Compact` (CLEF), next to or instead of the text file.
- Ship logs centrally (Seq, Application Insights, OpenTelemetry) by adding another provider in
  `LoggingHost.Create`. App code doesn't change.
- Add a "Send logs to support" command that zips `LogDirectory`.
