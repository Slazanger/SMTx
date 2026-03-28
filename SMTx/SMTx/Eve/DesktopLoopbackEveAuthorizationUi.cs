#if !NET8_0_BROWSER
using System.Diagnostics;
using System.Net;
using System.Text;
using SMTx.Eve.Connectors.Auth;

namespace SMTx.Eve;

/// <summary>Listens on the loopback redirect URI and opens the system browser for SSO.</summary>
public sealed class DesktopLoopbackEveAuthorizationUi : IEveAuthorizationUiCoordinator
{
    private readonly string _redirectUri;

    public DesktopLoopbackEveAuthorizationUi(string redirectUri)
    {
        _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
    }

    public async Task<Uri> StartAuthorizationAndWaitForCallbackAsync(Uri authorizeUri, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_redirectUri, UriKind.Absolute, out var redirect))
            throw new InvalidOperationException("Invalid EveOAuth:RedirectUri.");

        if (!string.Equals(redirect.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DesktopLoopbackEveAuthorizationUi requires an http:// loopback RedirectUri (e.g. http://127.0.0.1:8721/callback/).");

        var prefix = $"{redirect.Scheme}://{redirect.Authority}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizeUri.ToString(),
                UseShellExecute = true
            });

            var contextTask = listener.GetContextAsync();
            using var reg = cancellationToken.Register(() => listener.Close());
            var context = await contextTask.ConfigureAwait(false);
            var req = context.Request;
            var url = req.Url ?? throw new InvalidOperationException("Missing request URL.");

            var sb = new StringBuilder();
            sb.AppendLine("<html><body>OK — you can close this tab.</body></html>");
            var buf = Encoding.UTF8.GetBytes(sb.ToString());
            context.Response.ContentLength64 = buf.Length;
            context.Response.OutputStream.Write(buf, 0, buf.Length);
            context.Response.OutputStream.Close();

            return url;
        }
        finally
        {
            try { listener.Stop(); } catch { /* ignored */ }
        }
    }

}
#endif
