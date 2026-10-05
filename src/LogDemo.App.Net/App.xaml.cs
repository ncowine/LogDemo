using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using LogDemo.App.Net.Commands;
using LogDemo.App.Net.Logging;
using LogDemo.App.Net.Services;
using LogDemo.App.Net.ViewModels;
using LogDemo.App.Net.Views;
using LogDemo.Logging;
using LogDemo.Logging.Observability;
using LogDemo.Logging.Wpf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;

namespace LogDemo.App.Net;

/// <summary>
/// .NET 8 / .NET 10 host. Startup order matters:
/// <list type="number">
/// <item>configuration, then logging - before Prism builds the container, so startup failures are logged;</item>
/// <item>global exception handlers;</item>
/// <item>Prism: container registration, shell, initial navigation.</item>
/// </list>
/// </summary>
public partial class App : PrismApplication
{
    private const string ApplicationName = "LogDemo-Net";

    private readonly Stopwatch uptime = Stopwatch.StartNew();
    private AppPaths? paths;
    private IConfigurationRoot? configuration;
    private LoggingHost? logging;
    private GlobalExceptionHandler? exceptionHandler;
    private ILogger logger = NullLogger.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        Assembly entryAssembly = typeof(App).Assembly;
        AppConfigurationLoader configurationLoader = new AppConfigurationLoader();

        this.paths = new AppPaths(entryAssembly);
        this.configuration = configurationLoader.Build(this.paths, "LOGDEMO_");
        this.logging = LoggingHost.Create(
            this.configuration,
            ApplicationName,
            this.paths.DefaultLogDirectory,
            this.paths.Version,
            OpenTelemetryExport.FromConfiguration);
        this.logger = this.logging.LoggerFactory.CreateLogger<App>();
        this.exceptionHandler = GlobalExceptionHandler.Install(this, this.logging);

        this.logging.WriteSessionHeader(ApplicationName, entryAssembly);
        configurationLoader.AttachLogger(this.logger);
        _ = this.logging.StartRetentionCleanup();

        try
        {
            base.OnStartup(e); // Prism: container, RegisterTypes, CreateShell, OnInitialized
        }
        catch (Exception ex)
        {
            this.exceptionHandler.HandleStartupFailure(ex);
        }
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // Logging: ILogger<T> injectable everywhere.
        containerRegistry.RegisterLogging(this.logging!);
        containerRegistry.RegisterInstance<IConfiguration>(this.configuration!);
        containerRegistry.RegisterInstance(this.paths!);

        // Services
        containerRegistry.RegisterSingleton<DemoSettings>();
        containerRegistry.RegisterSingleton<ICustomerService, InMemoryCustomerService>();
        containerRegistry.RegisterSingleton<IUserNotificationService, MessageBoxNotificationService>();
        containerRegistry.RegisterSingleton<IShellLauncher, ShellLauncher>();

        // Commands: transient, each view model gets its own instance.
        containerRegistry.Register<NavigateCommand>();
        containerRegistry.Register<LoadCustomersCommand>();
        containerRegistry.Register<RemoveCustomerCommand>();
        containerRegistry.Register<OpenLogFolderCommand>();
        containerRegistry.Register<WriteSampleLogsCommand>();
        containerRegistry.Register<SimulateCrashCommand>();

        // Views (explicit view model mapping instead of relying on naming conventions)
        containerRegistry.RegisterForNavigation<CustomersView, CustomersViewModel>(ViewNames.Customers);
        containerRegistry.RegisterForNavigation<DiagnosticsView, DiagnosticsViewModel>(ViewNames.Diagnostics);
    }

    protected override void ConfigureViewModelLocator()
    {
        base.ConfigureViewModelLocator();
        ViewModelLocationProvider.Register<Shell, ShellViewModel>();
    }

    protected override Window CreateShell() => Container.Resolve<Shell>();

    protected override void OnInitialized()
    {
        Container.ValidateRegistrations(); // fail at startup, not on the first click

        base.OnInitialized(); // shows the shell
        Container.Resolve<NavigateCommand>().Execute(ViewNames.Customers);
        this.logger.StartupCompleted(this.uptime.ElapsedMilliseconds);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        this.logging?.WriteSessionFooter(ApplicationName, e.ApplicationExitCode);
        base.OnExit(e);
        this.exceptionHandler?.Dispose();
        this.logging?.Dispose(); // flush and close the file
    }
}
