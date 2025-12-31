using System.Text.Json;
using DataProcessor.Models;

namespace DataProcessor.Services;

public class JsonlParser
{
    public async Task<List<SolarSystem>> ParseSolarSystemsAsync(string sdeFolder)
    {
        var systemsPath = Path.Combine(sdeFolder, "mapSolarSystems.jsonl");
        
        if (!File.Exists(systemsPath))
        {
            throw new FileNotFoundException($"mapSolarSystems.jsonl not found at {systemsPath}");
        }

        var solarSystems = new List<SolarSystem>();
        Console.WriteLine($"Parsing mapSolarSystems.jsonl...");
        var lineCount = 0;

        await foreach (var line in File.ReadLinesAsync(systemsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonDoc = JsonDocument.Parse(line);
                var root = jsonDoc.RootElement;

                var system = new SolarSystem();

                // Extract _key as Id
                if (root.TryGetProperty("_key", out var keyElement))
                {
                    system.Id = keyElement.GetInt32();
                }

                // Extract name directly from mapSolarSystems.jsonl
                if (root.TryGetProperty("name", out var nameElement))
                {
                    if (nameElement.TryGetProperty("en", out var enNameElement))
                    {
                        system.Name = enNameElement.GetString();
                    }
                }

                // Extract regionID
                if (root.TryGetProperty("regionID", out var regionElement))
                {
                    system.RegionId = regionElement.GetInt32();
                }

                // Extract constellationID
                if (root.TryGetProperty("constellationID", out var constellationElement))
                {
                    system.ConstellationId = constellationElement.GetInt32();
                }

                // Extract factionID
                if (root.TryGetProperty("factionID", out var factionElement))
                {
                    system.FactionId = factionElement.GetInt32();
                }

                // Extract position (x, y, z)
                if (root.TryGetProperty("position", out var positionElement))
                {
                    if (positionElement.TryGetProperty("x", out var xElement))
                        system.PositionX = xElement.GetDecimal();
                    if (positionElement.TryGetProperty("y", out var yElement))
                        system.PositionY = yElement.GetDecimal();
                    if (positionElement.TryGetProperty("z", out var zElement))
                        system.PositionZ = zElement.GetDecimal();
                }

                // Extract position2D (x, y only - no z in 2D position)
                if (root.TryGetProperty("position2D", out var position2dElement))
                {
                    if (position2dElement.TryGetProperty("x", out var x2dElement))
                        system.Position2DX = x2dElement.GetDecimal();
                    if (position2dElement.TryGetProperty("y", out var y2dElement))
                        system.Position2DY = y2dElement.GetDecimal();
                }

                // Extract securityClass
                if (root.TryGetProperty("securityClass", out var securityClassElement))
                {
                    system.SecurityClass = securityClassElement.GetString();
                }

                // Extract securityStatus
                if (root.TryGetProperty("securityStatus", out var securityStatusElement))
                {
                    system.SecurityStatus = securityStatusElement.GetDecimal();
                }

                solarSystems.Add(system);
                lineCount++;

                if (lineCount % 1000 == 0)
                {
                    Console.WriteLine($"  Processed {lineCount} solar systems...");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Failed to parse line in mapSolarSystems.jsonl: {ex.Message}");
            }
        }

        Console.WriteLine($"Parsed {solarSystems.Count} solar systems.");
        return solarSystems;
    }
}

