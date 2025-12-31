using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text.Json;
using SMTx.Models;

namespace SMTx.Services;

public class RenderDatabaseReader
{
    private readonly string _dbPath;

    public RenderDatabaseReader(string dbPath)
    {
        _dbPath = dbPath;
    }

    public List<RenderSolarSystem> LoadSolarSystems()
    {
        var systems = new List<RenderSolarSystem>();

        if (!File.Exists(_dbPath))
        {
            return systems;
        }

        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        var query = "SELECT Id, Name, RenderCoords FROM RenderSolarSystems";
        using var command = new SQLiteCommand(query, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            var renderCoordsJson = reader.IsDBNull(2) ? null : reader.GetString(2);

            if (string.IsNullOrEmpty(renderCoordsJson))
                continue;

            try
            {
                // Configure JSON options to be case-insensitive
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var coords = JsonSerializer.Deserialize<CoordinateData>(renderCoordsJson, options);
                if (coords == null)
                    continue;

                // Debug: Log first few systems to see actual coordinates
                if (systems.Count < 5)
                {
                    System.Diagnostics.Debug.WriteLine($"System {id} ({name}): JSON={renderCoordsJson}, Parsed: X={coords.X}, Y={coords.Y}, Z={coords.Z}");
                }

                // Transform coordinates:
                // Screen X = World X (already correct)
                // Screen Y = -World Z (flip Z so +z points up)
                // World Y = depth (out of screen)
                systems.Add(new RenderSolarSystem
                {
                    Id = id,
                    Name = name,
                    ScreenX = coords.X,
                    ScreenY = -coords.Z, // Flip Z axis
                    WorldY = coords.Y
                });
            }
            catch (JsonException ex)
            {
                // Skip invalid JSON entries
                System.Diagnostics.Debug.WriteLine($"Failed to parse JSON for system {id}: {ex.Message}");
                continue;
            }
        }

        return systems;
    }

    private class CoordinateData
    {
        [System.Text.Json.Serialization.JsonPropertyName("x")]
        public double X { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("y")]
        public double Y { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("z")]
        public double Z { get; set; }
    }
}

