using Foundation;
using SMTx.Eve;
using SMTx.Eve.Connectors.Auth;
using UIKit;

namespace SMTx.iOS;

public sealed class IosEveAuthorizationUi : IEveAuthorizationUiCoordinator
{
    public Task<Uri> StartAuthorizationAndWaitForCallbackAsync(Uri authorizeUri, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        EveOAuthDeepLinkBridge.Pending = tcs;
        cancellationToken.Register(static s => ((TaskCompletionSource<Uri>)s!).TrySetCanceled(), tcs);

        UIApplication.SharedApplication.OpenUrl(new NSUrl(authorizeUri.ToString()), new UIApplicationOpenUrlOptions(), null);
        return tcs.Task;
    }
}
