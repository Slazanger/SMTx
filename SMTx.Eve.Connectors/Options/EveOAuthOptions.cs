namespace SMTx.Eve.Connectors.Options;

public sealed class EveOAuthOptions
{
    public const string SectionName = "EveOAuth";

    /// <summary>CCP application Client ID (prefer User Secrets / environment variables).</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Must match the callback URL registered for this client (per platform).</summary>
    public string RedirectUri { get; set; } = "";

    /// <summary>ESI scopes to request (e.g. esi-location.read_location.v1).</summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>Optional confidential client secret (not used for public PKCE desktop/mobile).</summary>
    public string? ClientSecret { get; set; }
}
