using System;
using LogDemo.App.NetFramework.Logging;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;
using Prism.Regions;

namespace LogDemo.App.NetFramework.Commands;

/// <summary>Navigates the main content region. Parameter: the view name (see <see cref="ViewNames"/>).</summary>
public sealed class NavigateCommand : CommandBase
{
    private readonly IRegionManager regionManager;

    public NavigateCommand(IRegionManager regionManager, ILogger<NavigateCommand> logger)
        : base(logger)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
    }

    protected override bool CanExecuteCore(object? parameter) => parameter is string target && target.Length > 0;

    protected override void ExecuteCore(object? parameter)
    {
        string target = (string)parameter!;

        // Prism reports navigation problems through the callback instead of throwing,
        // so without this they would never show up anywhere.
        this.regionManager.RequestNavigate(RegionNames.Content, target, result =>
        {
            if (result.Result == true)
            {
                Logger.NavigationSucceeded(target);
            }
            else
            {
                Logger.NavigationFailed(target, result.Error);
            }
        });
    }
}
