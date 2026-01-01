using System.IO;
using Android.Content.Res;

namespace SMTx.Android;

public static class AndroidAssetHelper
{
    public static bool CopyAssetToFile(AssetManager assetManager, string assetName, string destinationPath)
    {
        try
        {
            using var input = assetManager.Open(assetName);
            using var output = new FileStream(destinationPath, FileMode.Create);
            input.CopyTo(output);
            return true;
        }
        catch (Java.IO.IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy asset {assetName}: {ex.Message}");
            return false;
        }
    }
}

