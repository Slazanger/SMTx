using System.Text.Json;

namespace DataProcessor.Services;

public class TypesParser
{
    public async Task<Dictionary<int, string>> ParseTypesAsync(string sdeFolder)
    {
        var typesPath = Path.Combine(sdeFolder, "types.jsonl");
        
        if (!File.Exists(typesPath))
        {
            throw new FileNotFoundException($"types.jsonl not found at {typesPath}");
        }

        var nameLookup = new Dictionary<int, string>();

        Console.WriteLine($"Parsing types.jsonl...");
        var lineCount = 0;

        await foreach (var line in File.ReadLinesAsync(typesPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var jsonDoc = JsonDocument.Parse(line);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("_key", out var keyElement) && 
                    root.TryGetProperty("name", out var nameElement))
                {
                    var typeId = keyElement.GetInt32();
                    
                    if (nameElement.TryGetProperty("en", out var enNameElement))
                    {
                        var name = enNameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            nameLookup[typeId] = name;
                        }
                    }
                }

                lineCount++;
                if (lineCount % 10000 == 0)
                {
                    Console.WriteLine($"  Processed {lineCount} types...");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: Failed to parse line in types.jsonl: {ex.Message}");
            }
        }

        Console.WriteLine($"Parsed {nameLookup.Count} types with English names from {lineCount} total entries.");
        return nameLookup;
    }
}

