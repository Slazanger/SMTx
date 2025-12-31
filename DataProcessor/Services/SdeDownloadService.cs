using System.IO.Compression;
using System.Text.Json;

namespace DataProcessor.Services;

public class SdeDownloadService
{
    private const string LatestVersionUrl = "https://developers.eveonline.com/static-data/tranquility/latest.jsonl";
    private const string DownloadUrlTemplate = "https://developers.eveonline.com/static-data/tranquility/eve-online-static-data-{0}-jsonl.zip";
    private readonly string _dataExportPath;

    public SdeDownloadService(string dataExportPath)
    {
        _dataExportPath = dataExportPath;
    }

    public async Task<int> GetLatestBuildNumberAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync(LatestVersionUrl);
        
        // Parse JSONL (single line JSON)
        var jsonDoc = JsonDocument.Parse(response);
        var buildNumber = jsonDoc.RootElement.GetProperty("buildNumber").GetInt32();
        
        return buildNumber;
    }

    public async Task<string> DownloadAndExtractSdeAsync(int buildNumber)
    {
        var buildFolder = Path.Combine(_dataExportPath, buildNumber.ToString());
        
        // Check if folder already exists
        if (Directory.Exists(buildFolder))
        {
            Console.WriteLine($"SDE build {buildNumber} already exists at {buildFolder}. Skipping download.");
            return buildFolder;
        }

        var downloadUrl = string.Format(DownloadUrlTemplate, buildNumber);
        var zipPath = Path.Combine(Path.GetTempPath(), $"sde-{buildNumber}.zip");

        try
        {
            Console.WriteLine($"Downloading SDE build {buildNumber}...");
            using var httpClient = new HttpClient();
            var zipBytes = await httpClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, zipBytes);

            Console.WriteLine($"Extracting SDE to {buildFolder}...");
            Directory.CreateDirectory(buildFolder);
            ZipFile.ExtractToDirectory(zipPath, buildFolder);

            Console.WriteLine($"SDE extracted successfully.");
            return buildFolder;
        }
        finally
        {
            // Clean up temp zip file
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    public async Task<string> EnsureSdeDownloadedAsync()
    {
        var buildNumber = await GetLatestBuildNumberAsync();
        Console.WriteLine($"Latest SDE build number: {buildNumber}");
        
        var buildFolder = await DownloadAndExtractSdeAsync(buildNumber);
        return buildFolder;
    }
}

