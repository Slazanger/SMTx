using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMTx.Eve.Connectors;
using SMTx.Eve.Connectors.Auth;
using SMTx.Eve.Connectors.Options;
using SMTx.Eve.Connectors.Storage;

namespace SMTx.Eve;

public static class EveRuntime
{
    public static ICharacterSessionStore? Store { get; private set; }
    public static EveSsoService? Sso { get; private set; }
    public static EsiClientFacade? Esi { get; private set; }
    public static ILoggerFactory? LoggerFactory { get; private set; }

    public static bool IsAvailable => Sso != null && Store != null && Esi != null;

    public static async Task InitializeAsync(bool isBrowser, CancellationToken cancellationToken = default)
    {
        var config = EveConfigurationAccessor.Provider();
        if (config == null)
            return;

        var oauth = new EveOAuthOptions();
        config.GetSection(EveOAuthOptions.SectionName).Bind(oauth);
        var esi = new EveEsiOptions();
        config.GetSection(EveEsiOptions.SectionName).Bind(esi);

        try
        {
            EveOptionsValidation.Validate(oauth, esi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EVE config invalid: {ex.Message}");
            return;
        }

        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddDebug().SetMinimumLevel(LogLevel.Warning));
        var logSso = LoggerFactory.CreateLogger<EveSsoService>();
        var logEsi = LoggerFactory.CreateLogger<EsiClientFacade>();

        if (isBrowser)
        {
#if NET8_0_BROWSER
            Store = new BrowserCharacterSessionStore();
            var pkce = new BrowserOAuthPkceStore();
            await Store.LoadAsync(cancellationToken).ConfigureAwait(false);
            Sso = new EveSsoService(Options.Create(oauth), Options.Create(esi), Store, logSso, uiCoordinator: null, pkce, useBrowserSplitFlow: true);
            Esi = new EsiClientFacade(Options.Create(esi), Store, Sso, logEsi, LoggerFactory);

            var start = GetBrowserStartUri();
            if (start != null)
            {
                var summary = await Sso.TryCompleteBrowserAuthorizationAsync(start, cancellationToken).ConfigureAwait(false);
                if (summary != null)
                    StripOAuthQueryFromUrl();
            }

            await Sso.RestoreSessionsAsync(cancellationToken).ConfigureAwait(false);
#endif
        }
        else
        {
#if !NET8_0_BROWSER
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMTx", "eve-characters.json");
            Store = new FileCharacterSessionStore(path);
            await Store.LoadAsync(cancellationToken).ConfigureAwait(false);

            IEveAuthorizationUiCoordinator? ui = null;
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
                ui = EveMobileAuth.Coordinator;
            else if (oauth.RedirectUri.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
                     || oauth.RedirectUri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                ui = new DesktopLoopbackEveAuthorizationUi(oauth.RedirectUri);

            if (ui == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "EVE: No authorization UI. Desktop: use http://127.0.0.1:port/... RedirectUri. Android/iOS: set EveMobileAuth.Coordinator in platform startup.");
                return;
            }

            Sso = new EveSsoService(Options.Create(oauth), Options.Create(esi), Store, logSso, ui, null, useBrowserSplitFlow: false);
            Esi = new EsiClientFacade(Options.Create(esi), Store, Sso, logEsi, LoggerFactory);
            await Sso.RestoreSessionsAsync(cancellationToken).ConfigureAwait(false);
#endif
        }
    }

#if NET8_0_BROWSER
    private static Uri? GetBrowserStartUri()
    {
        try
        {
            using var location = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis.GetPropertyAsJSObject("location");
            if (location == null)
                return null;
            var href = location.GetPropertyAsString("href");
            return string.IsNullOrEmpty(href) ? null : new Uri(href);
        }
        catch
        {
            return null;
        }
    }

    private static void StripOAuthQueryFromUrl()
    {
        try
        {
            using var location = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis.GetPropertyAsJSObject("location");
            var path = location?.GetPropertyAsString("pathname") ?? "/";
            WasmNavigationInterop.ReplaceUrl(path);
        }
        catch
        {
            // ignored
        }
    }
#endif
}
