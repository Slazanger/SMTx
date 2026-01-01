using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SMTx.Models;

namespace SMTx.Services;

public class SqliteDataService : IDataService
{
    private readonly string _dbPath;

    public SqliteDataService(string dbPath)
    {
        _dbPath = dbPath;
    }

    public Task<List<RenderSolarSystem>> LoadSolarSystemsAsync()
    {
        var systems = new List<RenderSolarSystem>();

        if (!File.Exists(_dbPath))
        {
            return Task.FromResult(systems);
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var query = "SELECT Id, Name, RenderCoords FROM RenderSolarSystems";
        using var command = new SqliteCommand(query, connection);
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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var coords = JsonSerializer.Deserialize<CoordinateData>(renderCoordsJson, options);
                if (coords == null)
                    continue;

                systems.Add(new RenderSolarSystem
                {
                    Id = id,
                    Name = name,
                    WorldX = coords.X,
                    WorldY = coords.Y,
                    WorldZ = coords.Z
                });
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse JSON for system {id}: {ex.Message}");
                continue;
            }
        }

        return Task.FromResult(systems);
    }

    public Task<List<StargateLink>> LoadStargateLinksAsync()
    {
        var links = new List<StargateLink>();

        if (!File.Exists(_dbPath))
        {
            return Task.FromResult(links);
        }

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var query = "SELECT SourceSystemId, DestinationSystemId, LinkType FROM StargateLinks";
        using var command = new SqliteCommand(query, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var sourceId = reader.GetInt32(0);
            var destId = reader.GetInt32(1);
            var linkType = reader.IsDBNull(2) ? "regular" : reader.GetString(2);

            links.Add(new StargateLink
            {
                SourceSystemId = sourceId,
                DestinationSystemId = destId,
                LinkType = linkType ?? "regular"
            });
        }

        return Task.FromResult(links);
    }

    private class CoordinateData
    {
        [JsonPropertyName("x")]
        public double X { get; set; }
        
        [JsonPropertyName("y")]
        public double Y { get; set; }
        
        [JsonPropertyName("z")]
        public double Z { get; set; }
    }
}

