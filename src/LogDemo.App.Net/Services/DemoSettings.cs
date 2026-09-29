using System;
using LogDemo.App.Net.Logging;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;

namespace LogDemo.App.Net.Services;

/// <summary>Runtime switches for the demo (singleton, shared by the diagnostics page and the services).</summary>
public sealed class DemoSettings : BindableBase
{
    private readonly ILogger<DemoSettings> logger;
    private bool simulateBackendFailures;

    public DemoSettings(ILogger<DemoSettings> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool SimulateBackendFailures
    {
        get => this.simulateBackendFailures;
        set
        {
            if (SetProperty(ref this.simulateBackendFailures, value))
            {
                this.logger.SimulatedFailuresToggled(value ? "enabled" : "disabled");
            }
        }
    }
}
