using System;
using LogDemo.App.Net.Logging;
using LogDemo.Logging.Wpf.Commands;
using Microsoft.Extensions.Logging;
using Prism.Regions;

namespace LogDemo.App.Net.Commands;

/// <summary>Navigates the main content region. Parameter: the view name (see <see cref="ViewNames"/>).</summary>
public sealed class NavigateCommand : DelegateBaseCommand<string>
{
    private readonly IRegionManager regionManager;

    public NavigateCommand(IRegionManager regionManager, ILogger<NavigateCommand> logger)
        : base(logger)
    {
        this.regionManager = regionManager ?? throw new ArgumentNullException(nameof(regionManager));
    }

    protected override bool CanInvoke(string target) => !string.IsNullOrEmpty(target);

    protected override void Invoke(string target)
    {
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
