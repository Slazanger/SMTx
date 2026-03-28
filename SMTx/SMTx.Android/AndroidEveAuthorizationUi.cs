using Android.App;
using Android.Content;
using SMTx.Eve;
using SMTx.Eve.Connectors.Auth;

namespace SMTx.Android;

public sealed class AndroidEveAuthorizationUi : IEveAuthorizationUiCoordinator
{
    public Task<System.Uri> StartAuthorizationAndWaitForCallbackAsync(System.Uri authorizeUri, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<System.Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        EveOAuthDeepLinkBridge.Pending = tcs;
        cancellationToken.Register(static s => ((TaskCompletionSource<System.Uri>)s!).TrySetCanceled(), tcs);

        var intent = new Intent(Intent.ActionView);
        intent.SetData(global::Android.Net.Uri.Parse(authorizeUri.ToString()));
        intent.AddFlags(ActivityFlags.NewTask);
        Application.Context!.StartActivity(intent);
        return tcs.Task;
    }
}
