using System.Text.Json;
using DataProcessor.Models;

namespace DataProcessor.Services;

public class RegionParser
{
    public async Task<List<Region>> ParseRegionsAsync(string sdeFolder)
    {
        var regionsPath = Path.Combine(sdeFolder, "mapRegions.jsonl");
        
        if (!File.Exists(regionsPath))
        {
            throw new FileNotFoundException($"mapRegions.jsonl not found at {regionsPath}");
        }

        var regions = new List<Region>();
        Console.WriteLine($"Parsing mapRegions.jsonl...");
        var lineCount = 0;

        await foreach (var line in File.ReadLinesAsync(regionsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonDoc = JsonDocument.Parse(line);
                var root = jsonDoc.RootElement;

                var region = new Region();

                // Extract _key as Id
                if (root.TryGetProperty("_key", out var keyElement))
                {
                    region.Id = keyElement.GetInt32();
                }

                // Extract name.en
                if (root.TryGetProperty("name", out var nameElement))
                {
                    if (nameElement.TryGetProperty("en", out var enNameElement))
                    {
                        region.Name = enNameElement.GetString();
                    }
                }

                // Extract factionID
                if (root.TryGetProperty("factionID", out var factionElement))
                {
                    region.FactionId = factionElement.GetInt32();
                }

                // Extract position (x, y, z)
                if (root.TryGetProperty("position", out var positionElement))
                {
                    if (positionElement.TryGetProperty("x", out var xElement))
                        region.PositionX = xElement.GetDecimal();
                    if (positionElement.TryGetProperty("y", out var yElement))
                        region.PositionY = yElement.GetDecimal();
                    if (positionElement.TryGetProperty("z", out var zElement))
                        region.PositionZ = zElement.GetDecimal();
                }

                regions.Add(region);
                lineCount++;

                if (lineCount % 100 == 0)
                {
                    Console.WriteLine($"  Processed {lineCount} regions...");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Failed to parse line in mapRegions.jsonl: {ex.Message}");
            }
        }

        Console.WriteLine($"Parsed {regions.Count} regions.");
        return regions;
    }
}

