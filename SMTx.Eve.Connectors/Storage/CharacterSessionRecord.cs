namespace SMTx.Eve.Connectors.Storage;

public sealed class CharacterSessionRecord
{
    public long CharacterId { get; set; }
    public string CharacterName { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public DateTime AccessTokenExpiresUtc { get; set; }

    /// <summary>Space-separated ESI scopes from the JWT (for EVEStandard AuthDTO).</summary>
    public string Scopes { get; set; } = "";
}
