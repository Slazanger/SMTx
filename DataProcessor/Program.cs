using DataProcessor.Services;

namespace DataProcessor;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Get DataExport folder path (at solution root)
            // Try to find it by going up from current directory
            var currentDir = Directory.GetCurrentDirectory();
            var dataExportPath = currentDir;
            
            // Look for DataExport folder by going up the directory tree
            while (!Directory.Exists(Path.Combine(dataExportPath, "DataExport")) && 
                   Directory.GetParent(dataExportPath) != null)
            {
                dataExportPath = Directory.GetParent(dataExportPath)!.FullName;
            }
            
            dataExportPath = Path.Combine(dataExportPath, "DataExport");
            
            if (!Directory.Exists(dataExportPath))
            {
                // Fallback: create it at expected location (solution root)
                var solutionRoot = Path.GetFullPath(Path.Combine(currentDir, "..", ".."));
                dataExportPath = Path.Combine(solutionRoot, "DataExport");
                Directory.CreateDirectory(dataExportPath);
            }

            Console.WriteLine("=== EVE Online SDE Processor ===");
            Console.WriteLine($"DataExport path: {dataExportPath}");
            Console.WriteLine();

            // Initialize services
            var sdeDownloadService = new SdeDownloadService(dataExportPath);
            var jsonlParser = new JsonlParser();

            // Step 1: Get/download latest SDE
            var sdeFolder = await sdeDownloadService.EnsureSdeDownloadedAsync();
            Console.WriteLine();

            // Step 2: Process mapSolarSystems.jsonl (name is included in the file)
            var solarSystems = await jsonlParser.ParseSolarSystemsAsync(sdeFolder);
            Console.WriteLine();

            // Step 4: Store results in database
            var buildNumber = Path.GetFileName(sdeFolder);
            var dbPath = Path.Combine(sdeFolder, "solar_systems.db");
            var databaseService = new DatabaseService(dbPath);
            
            databaseService.InitializeDatabase();
            databaseService.InsertSolarSystems(solarSystems);
            Console.WriteLine();

            // Step 5: Display summary statistics
            Console.WriteLine("=== Processing Complete ===");
            Console.WriteLine($"Build Number: {buildNumber}");
            Console.WriteLine($"Total Solar Systems: {solarSystems.Count}");
            Console.WriteLine($"Systems with Names: {solarSystems.Count(s => !string.IsNullOrEmpty(s.Name))}");
            Console.WriteLine($"Database Location: {dbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
