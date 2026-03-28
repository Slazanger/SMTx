#if NET8_0_BROWSER
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace SMTx.Eve;

internal static partial class WasmLocalStorageInterop
{
    [JSImport("globalThis.smtxEveLocalStorage.getItem")]
    [SupportedOSPlatform("browser")]
    internal static partial string? GetItem(string key);

    [JSImport("globalThis.smtxEveLocalStorage.setItem")]
    [SupportedOSPlatform("browser")]
    internal static partial void SetItem(string key, string value);

    [JSImport("globalThis.smtxEveLocalStorage.removeItem")]
    [SupportedOSPlatform("browser")]
    internal static partial void RemoveItem(string key);
}

internal static partial class WasmSessionStorageInterop
{
    [JSImport("globalThis.smtxEveSessionStorage.getItem")]
    [SupportedOSPlatform("browser")]
    internal static partial string? GetItem(string key);

    [JSImport("globalThis.smtxEveSessionStorage.setItem")]
    [SupportedOSPlatform("browser")]
    internal static partial void SetItem(string key, string value);

    [JSImport("globalThis.smtxEveSessionStorage.removeItem")]
    [SupportedOSPlatform("browser")]
    internal static partial void RemoveItem(string key);
}

public static partial class WasmNavigationInterop
{
    [JSImport("globalThis.smtxNavigateTo")]
    [SupportedOSPlatform("browser")]
    public static partial void NavigateTo(string url);

    [JSImport("globalThis.smtxReplaceUrl")]
    [SupportedOSPlatform("browser")]
    internal static partial void ReplaceUrl(string url);
}
#endif
