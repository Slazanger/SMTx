using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Avalonia.ReactiveUI;

using System.IO;
using Foundation;
using Microsoft.Extensions.Configuration;
using SMTx.Eve;
using UIKit;

namespace SMTx.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    static IConfiguration? _eveConfiguration;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        EveMobileAuth.Coordinator = new IosEveAuthorizationUi();

        var path = NSBundle.MainBundle.PathForResource("appsettings", "json");
        _eveConfiguration = path != null && File.Exists(path)
            ? new ConfigurationBuilder().AddJsonFile(path).AddEnvironmentVariables().Build()
            : new ConfigurationBuilder().AddEnvironmentVariables().Build();
        EveConfigurationAccessor.Provider = () => _eveConfiguration!;

        if (this is IAvaloniaAppDelegate avaloniaDelegate)
        {
            avaloniaDelegate.Activated += (_, e) =>
            {
                if (e is ProtocolActivatedEventArgs p && p.Uri != null &&
                    p.Uri.AbsoluteUri.Contains("code=", StringComparison.Ordinal))
                    EveOAuthDeepLinkBridge.TryComplete(p.Uri);
            };
        }

        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI();
    }
}
