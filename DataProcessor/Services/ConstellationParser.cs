using System.Text.Json;
using DataProcessor.Models;

namespace DataProcessor.Services;

public class ConstellationParser
{
    public async Task<List<Constellation>> ParseConstellationsAsync(string sdeFolder)
    {
        var constellationsPath = Path.Combine(sdeFolder, "mapConstellations.jsonl");
        
        if (!File.Exists(constellationsPath))
        {
            throw new FileNotFoundException($"mapConstellations.jsonl not found at {constellationsPath}");
        }

        var constellations = new List<Constellation>();
        Console.WriteLine($"Parsing mapConstellations.jsonl...");
        var lineCount = 0;

        await foreach (var line in File.ReadLinesAsync(constellationsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonDoc = JsonDocument.Parse(line);
                var root = jsonDoc.RootElement;

                var constellation = new Constellation();

                // Extract _key as Id
                if (root.TryGetProperty("_key", out var keyElement))
                {
                    constellation.Id = keyElement.GetInt32();
                }

                // Extract name.en
                if (root.TryGetProperty("name", out var nameElement))
                {
                    if (nameElement.TryGetProperty("en", out var enNameElement))
                    {
                        constellation.Name = enNameElement.GetString();
                    }
                }

                // Extract regionID
                if (root.TryGetProperty("regionID", out var regionElement))
                {
                    constellation.RegionId = regionElement.GetInt32();
                }

                // Extract factionID
                if (root.TryGetProperty("factionID", out var factionElement))
                {
                    constellation.FactionId = factionElement.GetInt32();
                }

                // Extract position (x, y, z)
                if (root.TryGetProperty("position", out var positionElement))
                {
                    if (positionElement.TryGetProperty("x", out var xElement))
                        constellation.PositionX = xElement.GetDecimal();
                    if (positionElement.TryGetProperty("y", out var yElement))
                        constellation.PositionY = yElement.GetDecimal();
                    if (positionElement.TryGetProperty("z", out var zElement))
                        constellation.PositionZ = zElement.GetDecimal();
                }

                constellations.Add(constellation);
                lineCount++;

                if (lineCount % 100 == 0)
                {
                    Console.WriteLine($"  Processed {lineCount} constellations...");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Failed to parse line in mapConstellations.jsonl: {ex.Message}");
            }
        }

        Console.WriteLine($"Parsed {constellations.Count} constellations.");
        return constellations;
    }
}

