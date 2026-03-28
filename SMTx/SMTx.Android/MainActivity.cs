using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.IO;

using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;

using Microsoft.Extensions.Configuration;

using SMTx.Eve;

namespace SMTx.Android;

[Activity(
    Label = "SMTx.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = "smtx", DataHost = "oauth", DataPathPrefix = "/callback")]
public class MainActivity : AvaloniaMainActivity<App>
{
    static IConfiguration? _eveConfiguration;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        EveMobileAuth.Coordinator = new AndroidEveAuthorizationUi();
        if (_eveConfiguration == null && Assets != null)
        {
            try
            {
                using var stream = Assets.Open("appsettings.json");
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                _eveConfiguration = new ConfigurationBuilder()
                    .AddJsonStream(ms)
                    .AddEnvironmentVariables()
                    .Build();
            }
            catch
            {
                _eveConfiguration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            }

            EveConfigurationAccessor.Provider = () => _eveConfiguration;
        }

        HandleOAuthIntent(Intent);
        base.OnCreate(savedInstanceState);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleOAuthIntent(intent);
    }

    static void HandleOAuthIntent(Intent? intent)
    {
        var url = intent?.DataString;
        if (!string.IsNullOrEmpty(url) && url.Contains("code=", StringComparison.Ordinal))
            EveOAuthDeepLinkBridge.TryComplete(new Uri(url));
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI();
    }
}
