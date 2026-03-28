namespace SMTx.Eve.Connectors.Options;

public static class EveOptionsValidation
{
    public static void Validate(EveOAuthOptions oauth, EveEsiOptions esi)
    {
        if (string.IsNullOrWhiteSpace(oauth.ClientId))
            throw new InvalidOperationException("EveOAuth:ClientId is required (use User Secrets or environment variable EveOAuth__ClientId).");

        if (string.IsNullOrWhiteSpace(oauth.RedirectUri))
            throw new InvalidOperationException("EveOAuth:RedirectUri is required and must match the CCP developer portal.");

        if (oauth.Scopes == null || oauth.Scopes.Count == 0)
            throw new InvalidOperationException("EveOAuth:Scopes must contain at least one scope.");

        if (string.IsNullOrWhiteSpace(esi.UserAgent))
            throw new InvalidOperationException("EveEsi:UserAgent is required per CCP policy.");

        var redirect = oauth.RedirectUri.Trim();
        if (redirect.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !redirect.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
            && !redirect.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
        {
            // Non-loopback HTTP is unusual for OAuth redirects; warn only in debug builds.
            System.Diagnostics.Debug.WriteLine("EveOAuth:RedirectUri uses http:// on a non-loopback host; ensure this matches your CCP app registration.");
        }
    }
}
