using System.Text.Json;
using DataProcessor.Models;

namespace DataProcessor.Services;

public class StargateParser
{
    public async Task<List<Stargate>> ParseStargatesAsync(string sdeFolder)
    {
        var stargatesPath = Path.Combine(sdeFolder, "mapStargates.jsonl");
        
        if (!File.Exists(stargatesPath))
        {
            throw new FileNotFoundException($"mapStargates.jsonl not found at {stargatesPath}");
        }

        var stargates = new List<Stargate>();
        Console.WriteLine($"Parsing mapStargates.jsonl...");
        var lineCount = 0;

        await foreach (var line in File.ReadLinesAsync(stargatesPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonDoc = JsonDocument.Parse(line);
                var root = jsonDoc.RootElement;

                var stargate = new Stargate();

                // Extract _key as Id
                if (root.TryGetProperty("_key", out var keyElement))
                {
                    stargate.Id = keyElement.GetInt32();
                }

                // Extract solarSystemID as SourceSystemId
                if (root.TryGetProperty("solarSystemID", out var solarSystemElement))
                {
                    stargate.SourceSystemId = solarSystemElement.GetInt32();
                }

                // Extract destination information
                if (root.TryGetProperty("destination", out var destinationElement))
                {
                    if (destinationElement.TryGetProperty("solarSystemID", out var destSolarSystemElement))
                    {
                        stargate.DestinationSystemId = destSolarSystemElement.GetInt32();
                    }

                    if (destinationElement.TryGetProperty("stargateID", out var destStargateElement))
                    {
                        stargate.DestinationStargateId = destStargateElement.GetInt32();
                    }
                }

                // Only add stargate if it has required fields
                if (stargate.Id > 0 && stargate.SourceSystemId > 0 && stargate.DestinationSystemId > 0)
                {
                    stargates.Add(stargate);
                }
                lineCount++;

                if (lineCount % 1000 == 0)
                {
                    Console.WriteLine($"  Processed {lineCount} stargates...");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Failed to parse line in mapStargates.jsonl: {ex.Message}");
            }
        }

        Console.WriteLine($"Parsed {stargates.Count} stargates.");
        return stargates;
    }
}

