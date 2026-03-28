namespace SMTx.Eve.Connectors.Auth;

/// <summary>Preserves PKCE verifier across a full-page browser redirect (sessionStorage).</summary>
public interface IBrowserOAuthPkceStore
{
    void SaveVerifierForState(string state, string verifier);

    string? GetVerifierForState(string state);

    void ClearVerifierForState(string state);
}
