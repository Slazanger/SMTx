using SMTx.Eve.Connectors.Auth;

namespace SMTx.Eve;

/// <summary>Set from Android/iOS entry before <see cref="EveRuntime.InitializeAsync"/> if using a non-loopback redirect URI.</summary>
public static class EveMobileAuth
{
    public static IEveAuthorizationUiCoordinator? Coordinator { get; set; }
}
