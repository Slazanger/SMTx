#if NET8_0_BROWSER
using System.Runtime.Versioning;
using SMTx.Eve.Connectors.Auth;

namespace SMTx.Eve;

[SupportedOSPlatform("browser")]
public sealed class BrowserOAuthPkceStore : IBrowserOAuthPkceStore
{
    private static string Key(string state) => "smtx_oauth_pkce_" + state;

    public void SaveVerifierForState(string state, string verifier) =>
        WasmSessionStorageInterop.SetItem(Key(state), verifier);

    public string? GetVerifierForState(string state) =>
        WasmSessionStorageInterop.GetItem(Key(state));

    public void ClearVerifierForState(string state) =>
        WasmSessionStorageInterop.RemoveItem(Key(state));
}
#endif
