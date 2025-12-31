using System.Data.SQLite;
using DataProcessor.Models;
using DataProcessor.Services;

namespace DataProcessor.Services;

public class RenderDatabaseService
{
    private readonly string _dbPath;

    public RenderDatabaseService(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void InitializeDatabase()
    {
        // Create directory if it doesn't exist
        var dbDirectory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        // Delete existing database file if it exists
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
            Console.WriteLine($"Deleted existing render database at {_dbPath}");
        }

        // Create new database file
        SQLiteConnection.CreateFile(_dbPath);

        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        // Create RenderSolarSystems table
        var createSolarSystemsTableSql = @"
            CREATE TABLE IF NOT EXISTS RenderSolarSystems (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                RenderCoords TEXT
            )";

        using var command1 = new SQLiteCommand(createSolarSystemsTableSql, connection);
        command1.ExecuteNonQuery();

        // Create RenderRegions table
        var createRegionsTableSql = @"
            CREATE TABLE IF NOT EXISTS RenderRegions (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                RenderCoords TEXT
            )";

        using var command2 = new SQLiteCommand(createRegionsTableSql, connection);
        command2.ExecuteNonQuery();

        // Create RenderConstellations table
        var createConstellationsTableSql = @"
            CREATE TABLE IF NOT EXISTS RenderConstellations (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                RenderCoords TEXT
            )";

        using var command3 = new SQLiteCommand(createConstellationsTableSql, connection);
        command3.ExecuteNonQuery();

        // Create StargateLinks table
        var createStargateLinksTableSql = @"
            CREATE TABLE IF NOT EXISTS StargateLinks (
                SourceSystemId INTEGER NOT NULL,
                DestinationSystemId INTEGER NOT NULL,
                LinkType TEXT NOT NULL,
                PRIMARY KEY (SourceSystemId, DestinationSystemId)
            )";

        using var command4 = new SQLiteCommand(createStargateLinksTableSql, connection);
        command4.ExecuteNonQuery();

        // Create ConstellationLinks table
        var createConstellationLinksTableSql = @"
            CREATE TABLE IF NOT EXISTS ConstellationLinks (
                SourceConstellationId INTEGER NOT NULL,
                DestinationConstellationId INTEGER NOT NULL,
                PRIMARY KEY (SourceConstellationId, DestinationConstellationId)
            )";

        using var command5 = new SQLiteCommand(createConstellationLinksTableSql, connection);
        command5.ExecuteNonQuery();

        Console.WriteLine($"Render database initialized at {_dbPath}");
    }

    public void InsertSolarSystems(List<SolarSystem> systems, Dictionary<int, string> renderCoords)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO RenderSolarSystems (Id, Name, RenderCoords)
            VALUES (@Id, @Name, @RenderCoords)";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@Id"));
        command.Parameters.Add(new SQLiteParameter("@Name"));
        command.Parameters.Add(new SQLiteParameter("@RenderCoords"));

        var inserted = 0;
        foreach (var system in systems)
        {
            if (!renderCoords.TryGetValue(system.Id, out var coords))
                continue;

            command.Parameters["@Id"].Value = system.Id;
            command.Parameters["@Name"].Value = (object?)system.Name ?? DBNull.Value;
            command.Parameters["@RenderCoords"].Value = coords;

            command.ExecuteNonQuery();
            inserted++;

            if (inserted % 1000 == 0)
            {
                Console.WriteLine($"  Inserted {inserted} solar systems...");
            }
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} solar systems into render database.");
    }

    public void InsertRegions(List<Region> regions, Dictionary<int, string> renderCoords)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO RenderRegions (Id, Name, RenderCoords)
            VALUES (@Id, @Name, @RenderCoords)";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@Id"));
        command.Parameters.Add(new SQLiteParameter("@Name"));
        command.Parameters.Add(new SQLiteParameter("@RenderCoords"));

        var inserted = 0;
        foreach (var region in regions)
        {
            if (!renderCoords.TryGetValue(region.Id, out var coords))
                continue;

            command.Parameters["@Id"].Value = region.Id;
            command.Parameters["@Name"].Value = (object?)region.Name ?? DBNull.Value;
            command.Parameters["@RenderCoords"].Value = coords;

            command.ExecuteNonQuery();
            inserted++;
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} regions into render database.");
    }

    public void InsertConstellations(List<Constellation> constellations, Dictionary<int, string> renderCoords)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO RenderConstellations (Id, Name, RenderCoords)
            VALUES (@Id, @Name, @RenderCoords)";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@Id"));
        command.Parameters.Add(new SQLiteParameter("@Name"));
        command.Parameters.Add(new SQLiteParameter("@RenderCoords"));

        var inserted = 0;
        foreach (var constellation in constellations)
        {
            if (!renderCoords.TryGetValue(constellation.Id, out var coords))
                continue;

            command.Parameters["@Id"].Value = constellation.Id;
            command.Parameters["@Name"].Value = (object?)constellation.Name ?? DBNull.Value;
            command.Parameters["@RenderCoords"].Value = coords;

            command.ExecuteNonQuery();
            inserted++;
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} constellations into render database.");
    }

    public void InsertStargateLinks(List<LinkProcessor.StargateLink> links)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO StargateLinks (SourceSystemId, DestinationSystemId, LinkType)
            VALUES (@SourceSystemId, @DestinationSystemId, @LinkType)";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@SourceSystemId"));
        command.Parameters.Add(new SQLiteParameter("@DestinationSystemId"));
        command.Parameters.Add(new SQLiteParameter("@LinkType"));

        var inserted = 0;
        foreach (var link in links)
        {
            command.Parameters["@SourceSystemId"].Value = link.SourceSystemId;
            command.Parameters["@DestinationSystemId"].Value = link.DestinationSystemId;
            command.Parameters["@LinkType"].Value = link.LinkType;

            command.ExecuteNonQuery();
            inserted++;

            if (inserted % 1000 == 0)
            {
                Console.WriteLine($"  Inserted {inserted} stargate links...");
            }
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} stargate links into render database.");
    }

    public void InsertConstellationLinks(List<LinkProcessor.ConstellationLink> links)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO ConstellationLinks (SourceConstellationId, DestinationConstellationId)
            VALUES (@SourceConstellationId, @DestinationConstellationId)";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@SourceConstellationId"));
        command.Parameters.Add(new SQLiteParameter("@DestinationConstellationId"));

        var inserted = 0;
        foreach (var link in links)
        {
            command.Parameters["@SourceConstellationId"].Value = link.SourceConstellationId;
            command.Parameters["@DestinationConstellationId"].Value = link.DestinationConstellationId;

            command.ExecuteNonQuery();
            inserted++;
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} constellation links into render database.");
    }
}

