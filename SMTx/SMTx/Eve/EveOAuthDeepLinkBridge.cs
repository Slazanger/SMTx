namespace SMTx.Eve;

/// <summary>Android / iOS deep-link return delivers the callback URI here.</summary>
public static class EveOAuthDeepLinkBridge
{
    public static TaskCompletionSource<Uri>? Pending { get; set; }

    public static void TryComplete(Uri uri) => Pending?.TrySetResult(uri);
}
