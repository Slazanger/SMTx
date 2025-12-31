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

        var createTableSql = @"
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

        using var command = new SQLiteCommand(createTableSql, connection);
        command.ExecuteNonQuery();

        // Clear existing data if re-running
        var clearSql = "DELETE FROM SolarSystems";
        using var clearCommand = new SQLiteCommand(clearSql, connection);
        clearCommand.ExecuteNonQuery();

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
}

