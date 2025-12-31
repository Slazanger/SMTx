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
            var stargateParser = new StargateParser();
            var regionParser = new RegionParser();
            var constellationParser = new ConstellationParser();

            // Step 1: Get/download latest SDE
            var sdeFolder = await sdeDownloadService.EnsureSdeDownloadedAsync();
            Console.WriteLine();

            // Step 2: Process mapSolarSystems.jsonl (name is included in the file)
            var solarSystems = await jsonlParser.ParseSolarSystemsAsync(sdeFolder);
            Console.WriteLine();

            // Step 3: Process mapStargates.jsonl to extract stargate connections
            var stargates = await stargateParser.ParseStargatesAsync(sdeFolder);
            Console.WriteLine();

            // Step 4: Process mapRegions.jsonl to extract region data
            var regions = await regionParser.ParseRegionsAsync(sdeFolder);
            Console.WriteLine();

            // Step 5: Process mapConstellations.jsonl to extract constellation data
            var constellations = await constellationParser.ParseConstellationsAsync(sdeFolder);
            Console.WriteLine();

            // Step 6: Store results in database
            var buildNumber = Path.GetFileName(sdeFolder);
            var dbPath = Path.Combine(sdeFolder, "universe.db");
            var databaseService = new DatabaseService(dbPath);
            
            databaseService.InitializeDatabase();
            databaseService.InsertSolarSystems(solarSystems);
            databaseService.InsertStargates(stargates);
            databaseService.InsertRegions(regions);
            databaseService.InsertConstellations(constellations);
            Console.WriteLine();

            // Step 7: Generate render database
            Console.WriteLine("=== Generating Render Database ===");
            var renderDbPath = Path.Combine(sdeFolder, "render.db");
            var renderDbService = new RenderDatabaseService(renderDbPath);
            var coordinateScaler = new CoordinateScaler();
            var linkProcessor = new LinkProcessor();

            // Filter out regions with ID >= 11000001 and their associated systems/constellations

            // ignore the following regionss
            var regionsToIgnore = new List<int> 
                {   
                    // CCP Regions
                    10000004,
                    10000017,
                    10000019,

                    // wormhold regions
                    11000001, 
                    11000002,
                    11000003, 
                    11000004, 
                    11000005, 
                    11000006, 
                    11000007, 
                    11000008, 
                    11000009, 
                    11000010, 
                    11000011, 
                    11000012, 
                    11000013, 
                    11000014, 
                    11000015, 
                    11000016, 
                    11000017, 
                    11000018, 
                    11000019, 
                    11000020, 
                    11000021, 
                    11000022, 
                    11000023, 
                    11000024, 
                    11000025, 
                    11000026, 
                    11000027,
                    11000028, 
                    11000029, 
                    11000030, 
                    11000031, 
                    11000032, 
                    11000033,

                    // ADR
                    12000001, 
                    12000002,
                    12000003,
                    12000004,
                    12000005,

                    // VR- regions
                    14000001,
                    14000002,
                    14000003,
                    14000004,
                    14000005,

                    // GPM
                    19000001
                    };
            


            Console.WriteLine("Filtering data for render database...");


            var filteredRegions = regions.Where(r => !regionsToIgnore.Contains(r.Id)).ToList();
            var filteredSystems = solarSystems.Where(s => !s.RegionId.HasValue || !regionsToIgnore.Contains(s.RegionId.Value)).ToList();
            var filteredConstellations = constellations.Where(c => !c.RegionId.HasValue || !regionsToIgnore.Contains(c.RegionId.Value)).ToList();
            
            Console.WriteLine($"Filtered regions: {filteredRegions.Count} (from {regions.Count})");
            Console.WriteLine($"Filtered systems: {filteredSystems.Count} (from {solarSystems.Count})");
            Console.WriteLine($"Filtered constellations: {filteredConstellations.Count} (from {constellations.Count})");
            Console.WriteLine();

            // Filter stargates to only include links between filtered systems
            var filteredSystemIds = new HashSet<int>(filteredSystems.Select(s => s.Id));
            var filteredStargates = stargates.Where(s => 
                filteredSystemIds.Contains(s.SourceSystemId) && 
                filteredSystemIds.Contains(s.DestinationSystemId)).ToList();
            Console.WriteLine($"Filtered stargates: {filteredStargates.Count} (from {stargates.Count})");
            Console.WriteLine();

            // Calculate bounding boxes
            Console.WriteLine("Calculating bounding boxes...");
            var systemBounds = coordinateScaler.CalculateBoundingBox(filteredSystems);
            var regionBounds = coordinateScaler.CalculateBoundingBox(filteredRegions);
            var constellationBounds = coordinateScaler.CalculateBoundingBox(filteredConstellations);
            Console.WriteLine($"System bounds: X[{systemBounds.MinX:F2}, {systemBounds.MaxX:F2}], Y[{systemBounds.MinY:F2}, {systemBounds.MaxY:F2}], Z[{systemBounds.MinZ:F2}, {systemBounds.MaxZ:F2}]");
            Console.WriteLine();

            // Scale coordinates for solar systems
            Console.WriteLine("Scaling solar system coordinates...");
            var systemRenderCoords = new Dictionary<int, string>();
            foreach (var system in filteredSystems)
            {
                var coords = coordinateScaler.ScaleCoordinates(system.PositionX, system.PositionY, system.PositionZ, systemBounds);
                systemRenderCoords[system.Id] = coords;
            }

            // Scale coordinates for regions
            Console.WriteLine("Scaling region coordinates...");
            var regionRenderCoords = new Dictionary<int, string>();
            foreach (var region in filteredRegions)
            {
                var coords = coordinateScaler.ScaleCoordinates(region.PositionX, region.PositionY, region.PositionZ, regionBounds);
                regionRenderCoords[region.Id] = coords;
            }

            // Scale coordinates for constellations
            Console.WriteLine("Scaling constellation coordinates...");
            var constellationRenderCoords = new Dictionary<int, string>();
            foreach (var constellation in filteredConstellations)
            {
                var coords = coordinateScaler.ScaleCoordinates(constellation.PositionX, constellation.PositionY, constellation.PositionZ, constellationBounds);
                constellationRenderCoords[constellation.Id] = coords;
            }
            Console.WriteLine();

            // Process stargate links
            Console.WriteLine("Processing stargate links...");
            var systemLookup = filteredSystems.ToDictionary(s => s.Id);
            var stargateLinks = linkProcessor.ProcessStargateLinks(filteredStargates, systemLookup);
            Console.WriteLine($"Generated {stargateLinks.Count} unique stargate links");
            Console.WriteLine($"  Regular: {stargateLinks.Count(l => l.LinkType == "regular")}");
            Console.WriteLine($"  Constellation: {stargateLinks.Count(l => l.LinkType == "constellation")}");
            Console.WriteLine($"  Regional: {stargateLinks.Count(l => l.LinkType == "regional")}");
            Console.WriteLine();

            // Calculate constellation links
            Console.WriteLine("Calculating constellation links...");
            var constellationLinks = linkProcessor.CalculateConstellationLinks(stargateLinks, systemLookup);
            Console.WriteLine($"Generated {constellationLinks.Count} unique constellation links");
            Console.WriteLine();

            // Create render database
            renderDbService.InitializeDatabase();
            renderDbService.InsertSolarSystems(filteredSystems, systemRenderCoords);
            renderDbService.InsertRegions(filteredRegions, regionRenderCoords);
            renderDbService.InsertConstellations(filteredConstellations, constellationRenderCoords);
            renderDbService.InsertStargateLinks(stargateLinks);
            renderDbService.InsertConstellationLinks(constellationLinks);
            Console.WriteLine();

            // Step 8: Display summary statistics
            Console.WriteLine("=== Processing Complete ===");
            Console.WriteLine($"Build Number: {buildNumber}");
            Console.WriteLine($"Total Solar Systems: {solarSystems.Count}");
            Console.WriteLine($"Systems with Names: {solarSystems.Count(s => !string.IsNullOrEmpty(s.Name))}");
            Console.WriteLine($"Total Stargates: {stargates.Count}");
            Console.WriteLine($"Unique Connections: {stargates.Select(s => new { s.SourceSystemId, s.DestinationSystemId }).Distinct().Count()}");
            Console.WriteLine($"Total Regions: {regions.Count}");
            Console.WriteLine($"Regions with Names: {regions.Count(r => !string.IsNullOrEmpty(r.Name))}");
            Console.WriteLine($"Total Constellations: {constellations.Count}");
            Console.WriteLine($"Constellations with Names: {constellations.Count(c => !string.IsNullOrEmpty(c.Name))}");
            Console.WriteLine($"Universe Database Location: {dbPath}");
            Console.WriteLine($"Render Database Location: {renderDbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
