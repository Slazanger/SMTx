using System.Text;
using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Configuration;
using SMTx;
using SMTx.Eve;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        var startUrl = args.Length > 0 ? args[0] : "http://localhost/";
        var root = new Uri(startUrl).GetLeftPart(UriPartial.Authority) + "/";

        var configBuilder = new ConfigurationBuilder().AddEnvironmentVariables();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(root) };
            var json = await http.GetStringAsync("appsettings.json").ConfigureAwait(false);
            configBuilder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        }
        catch
        {
            // Optional file — env vars may still supply EveOAuth__ClientId etc.
        }

        EveConfigurationAccessor.Provider = () => configBuilder.Build();

        await BuildAvaloniaApp()
            .WithInterFont()
            .UseReactiveUI()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
