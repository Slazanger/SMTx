using System.Data.SQLite;
using DataProcessor.Models;

namespace DataProcessor.Services;

public class DatabaseService
{
    private readonly string _dbPath;

    public DatabaseService(string dbPath)
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

        // Create database file if it doesn't exist
        if (!File.Exists(_dbPath))
        {
            SQLiteConnection.CreateFile(_dbPath);
        }

        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        var createSolarSystemsTableSql = @"
            CREATE TABLE IF NOT EXISTS SolarSystems (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                ConstellationId INTEGER,
                FactionId INTEGER,
                PositionX REAL,
                PositionY REAL,
                PositionZ REAL,
                Position2DX REAL,
                Position2DY REAL,
                Position2DZ REAL,
                SecurityClass TEXT,
                SecurityStatus REAL
            )";

        using var command1 = new SQLiteCommand(createSolarSystemsTableSql, connection);
        command1.ExecuteNonQuery();

        var createStargatesTableSql = @"
            CREATE TABLE IF NOT EXISTS Stargates (
                Id INTEGER PRIMARY KEY,
                SourceSystemId INTEGER NOT NULL,
                DestinationSystemId INTEGER NOT NULL,
                DestinationStargateId INTEGER,
                FOREIGN KEY (SourceSystemId) REFERENCES SolarSystems(Id),
                FOREIGN KEY (DestinationSystemId) REFERENCES SolarSystems(Id)
            )";

        using var command2 = new SQLiteCommand(createStargatesTableSql, connection);
        command2.ExecuteNonQuery();

        var createRegionsTableSql = @"
            CREATE TABLE IF NOT EXISTS Regions (
                Id INTEGER PRIMARY KEY,
                Name TEXT,
                FactionId INTEGER,
                PositionX REAL,
                PositionY REAL,
                PositionZ REAL
            )";

        using var command3 = new SQLiteCommand(createRegionsTableSql, connection);
        command3.ExecuteNonQuery();

        // Clear existing data if re-running
        var clearStargatesSql = "DELETE FROM Stargates";
        using var clearStargatesCommand = new SQLiteCommand(clearStargatesSql, connection);
        clearStargatesCommand.ExecuteNonQuery();

        var clearSolarSystemsSql = "DELETE FROM SolarSystems";
        using var clearSolarSystemsCommand = new SQLiteCommand(clearSolarSystemsSql, connection);
        clearSolarSystemsCommand.ExecuteNonQuery();

        var clearRegionsSql = "DELETE FROM Regions";
        using var clearRegionsCommand = new SQLiteCommand(clearRegionsSql, connection);
        clearRegionsCommand.ExecuteNonQuery();

        Console.WriteLine($"Database initialized at {_dbPath}");
    }

    public void InsertSolarSystems(List<SolarSystem> solarSystems)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO SolarSystems (
                Id, Name, ConstellationId, FactionId,
                PositionX, PositionY, PositionZ,
                Position2DX, Position2DY, Position2DZ,
                SecurityClass, SecurityStatus
            ) VALUES (
                @Id, @Name, @ConstellationId, @FactionId,
                @PositionX, @PositionY, @PositionZ,
                @Position2DX, @Position2DY, @Position2DZ,
                @SecurityClass, @SecurityStatus
            )";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@Id"));
        command.Parameters.Add(new SQLiteParameter("@Name"));
        command.Parameters.Add(new SQLiteParameter("@ConstellationId"));
        command.Parameters.Add(new SQLiteParameter("@FactionId"));
        command.Parameters.Add(new SQLiteParameter("@PositionX"));
        command.Parameters.Add(new SQLiteParameter("@PositionY"));
        command.Parameters.Add(new SQLiteParameter("@PositionZ"));
        command.Parameters.Add(new SQLiteParameter("@Position2DX"));
        command.Parameters.Add(new SQLiteParameter("@Position2DY"));
        command.Parameters.Add(new SQLiteParameter("@Position2DZ"));
        command.Parameters.Add(new SQLiteParameter("@SecurityClass"));
        command.Parameters.Add(new SQLiteParameter("@SecurityStatus"));

        var inserted = 0;
        foreach (var system in solarSystems)
        {
            command.Parameters["@Id"].Value = system.Id;
            command.Parameters["@Name"].Value = (object?)system.Name ?? DBNull.Value;
            command.Parameters["@ConstellationId"].Value = (object?)system.ConstellationId ?? DBNull.Value;
            command.Parameters["@FactionId"].Value = (object?)system.FactionId ?? DBNull.Value;
            command.Parameters["@PositionX"].Value = (object?)system.PositionX ?? DBNull.Value;
            command.Parameters["@PositionY"].Value = (object?)system.PositionY ?? DBNull.Value;
            command.Parameters["@PositionZ"].Value = (object?)system.PositionZ ?? DBNull.Value;
            command.Parameters["@Position2DX"].Value = (object?)system.Position2DX ?? DBNull.Value;
            command.Parameters["@Position2DY"].Value = (object?)system.Position2DY ?? DBNull.Value;
            command.Parameters["@Position2DZ"].Value = (object?)system.Position2DZ ?? DBNull.Value;
            command.Parameters["@SecurityClass"].Value = (object?)system.SecurityClass ?? DBNull.Value;
            command.Parameters["@SecurityStatus"].Value = (object?)system.SecurityStatus ?? DBNull.Value;

            command.ExecuteNonQuery();
            inserted++;

            if (inserted % 1000 == 0)
            {
                Console.WriteLine($"  Inserted {inserted} solar systems...");
            }
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} solar systems into database.");
    }

    public void InsertStargates(List<Stargate> stargates)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO Stargates (
                Id, SourceSystemId, DestinationSystemId, DestinationStargateId
            ) VALUES (
                @Id, @SourceSystemId, @DestinationSystemId, @DestinationStargateId
            )";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@Id"));
        command.Parameters.Add(new SQLiteParameter("@SourceSystemId"));
        command.Parameters.Add(new SQLiteParameter("@DestinationSystemId"));
        command.Parameters.Add(new SQLiteParameter("@DestinationStargateId"));

        var inserted = 0;
        foreach (var stargate in stargates)
        {
            command.Parameters["@Id"].Value = stargate.Id;
            command.Parameters["@SourceSystemId"].Value = stargate.SourceSystemId;
            command.Parameters["@DestinationSystemId"].Value = stargate.DestinationSystemId;
            command.Parameters["@DestinationStargateId"].Value = (object?)stargate.DestinationStargateId ?? DBNull.Value;

            command.ExecuteNonQuery();
            inserted++;

            if (inserted % 1000 == 0)
            {
                Console.WriteLine($"  Inserted {inserted} stargates...");
            }
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} stargates into database.");
    }

    public void InsertRegions(List<Region> regions)
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        var insertSql = @"
            INSERT INTO Regions (
                Id, Name, FactionId, PositionX, PositionY, PositionZ
            ) VALUES (
                @Id, @Name, @FactionId, @PositionX, @PositionY, @PositionZ
            )";

        using var command = new SQLiteCommand(insertSql, connection, transaction);
        
        command.Parameters.Add(new SQLiteParameter("@Id"));
        command.Parameters.Add(new SQLiteParameter("@Name"));
        command.Parameters.Add(new SQLiteParameter("@FactionId"));
        command.Parameters.Add(new SQLiteParameter("@PositionX"));
        command.Parameters.Add(new SQLiteParameter("@PositionY"));
        command.Parameters.Add(new SQLiteParameter("@PositionZ"));

        var inserted = 0;
        foreach (var region in regions)
        {
            command.Parameters["@Id"].Value = region.Id;
            command.Parameters["@Name"].Value = (object?)region.Name ?? DBNull.Value;
            command.Parameters["@FactionId"].Value = (object?)region.FactionId ?? DBNull.Value;
            command.Parameters["@PositionX"].Value = (object?)region.PositionX ?? DBNull.Value;
            command.Parameters["@PositionY"].Value = (object?)region.PositionY ?? DBNull.Value;
            command.Parameters["@PositionZ"].Value = (object?)region.PositionZ ?? DBNull.Value;

            command.ExecuteNonQuery();
            inserted++;

            if (inserted % 50 == 0)
            {
                Console.WriteLine($"  Inserted {inserted} regions...");
            }
        }

        transaction.Commit();
        Console.WriteLine($"Successfully inserted {inserted} regions into database.");
    }
}

