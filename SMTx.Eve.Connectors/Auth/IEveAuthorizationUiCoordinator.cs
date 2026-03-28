namespace SMTx.Eve.Connectors.Auth;

/// <summary>Opens the system/browser SSO page and returns the full redirect callback URI (query includes code and state).</summary>
public interface IEveAuthorizationUiCoordinator
{
    Task<Uri> StartAuthorizationAndWaitForCallbackAsync(Uri authorizeUri, CancellationToken cancellationToken = default);
}
