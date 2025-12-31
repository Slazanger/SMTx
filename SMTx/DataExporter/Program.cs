using System;
using System.IO;
using System.Text.Json;
using SMTx.Services;

namespace DataExporter;

class Program
{
    static void Main(string[] args)
    {
        // Find the database
        var currentDir = Directory.GetCurrentDirectory();
        var workspaceRoot = currentDir;
        
        var directory = new DirectoryInfo(currentDir);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "DataExport")))
        {
            directory = directory.Parent;
        }
        
        if (directory != null)
        {
            workspaceRoot = directory.FullName;
        }
        
        var dbPath = Path.Combine(workspaceRoot, "DataExport", "3142455", "render.db");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Database not found at: {dbPath}");
            Console.WriteLine("Please ensure render.db exists in DataExport/3142455/");
            return;
        }

        Console.WriteLine($"Exporting data from: {dbPath}");

        // Create output directory
        var outputDir = Path.Combine(workspaceRoot, "SMTx", "SMTx.Browser", "wwwroot", "data");
        Directory.CreateDirectory(outputDir);

        // Load data
        var dataService = new SqliteDataService(dbPath);
        var systems = dataService.LoadSolarSystemsAsync().Result;
        var links = dataService.LoadStargateLinksAsync().Result;

        Console.WriteLine($"Loaded {systems.Count} solar systems and {links.Count} stargate links");

        // Export to JSON
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var systemsJson = JsonSerializer.Serialize(systems, options);
        var linksJson = JsonSerializer.Serialize(links, options);

        var systemsPath = Path.Combine(outputDir, "solar-systems.json");
        var linksPath = Path.Combine(outputDir, "stargate-links.json");

        File.WriteAllText(systemsPath, systemsJson);
        File.WriteAllText(linksPath, linksJson);

        Console.WriteLine($"Exported to:");
        Console.WriteLine($"  {systemsPath}");
        Console.WriteLine($"  {linksPath}");
        Console.WriteLine("Done!");
    }
}

