using AppTrace.Core.Models;
using Microsoft.Data.Sqlite;

namespace AppTrace.Core.Data;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task LogActivityAsync(ActivityLogEntry entry);
    Task<List<ActivityLogEntry>> GetActivitiesAsync(int limit = 100, string? actionType = null, string? search = null);
    Task ClearActivitiesAsync();
    Task AddQuarantineItemAsync(QuarantineItem item);
    Task<List<QuarantineItem>> GetQuarantineItemsAsync(bool includeRestored = false);
    Task UpdateQuarantineItemAsync(QuarantineItem item);
    Task DeleteQuarantineItemAsync(string id);
}

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;
    private readonly string _dbDirectory;
    private bool _initialized;

    public DatabaseService(string? customDbPath = null)
    {
        if (!string.IsNullOrEmpty(customDbPath))
        {
            _dbDirectory = Path.GetDirectoryName(customDbPath) ?? string.Empty;
            _connectionString = $"Data Source={customDbPath}";
        }
        else
        {
            _dbDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppTrace");
            string dbPath = Path.Combine(_dbDirectory, "apptrace.db");
            _connectionString = $"Data Source={dbPath}";
        }
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        if (!string.IsNullOrEmpty(_dbDirectory) && !Directory.Exists(_dbDirectory))
        {
            Directory.CreateDirectory(_dbDirectory);
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;

            CREATE TABLE IF NOT EXISTS ActivityLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                ApplicationName TEXT NOT NULL,
                Description TEXT NOT NULL,
                Details TEXT,
                Success INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Quarantine (
                Id TEXT PRIMARY KEY,
                OriginalPath TEXT NOT NULL,
                BackupPath TEXT NOT NULL,
                OperationDate TEXT NOT NULL,
                RelatedAppName TEXT NOT NULL,
                ItemType TEXT NOT NULL,
                SizeBytes INTEGER NOT NULL,
                ManifestJson TEXT,
                IsRestored INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_activity_time ON ActivityLog (Timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_quarantine_date ON Quarantine (OperationDate DESC);
        ";

        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        _initialized = true;
    }

    public async Task LogActivityAsync(ActivityLogEntry entry)
    {
        await InitializeAsync();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            INSERT INTO ActivityLog (Timestamp, ActionType, ApplicationName, Description, Details, Success)
            VALUES (@timestamp, @actionType, @appName, @desc, @details, @success);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@timestamp", entry.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("@actionType", entry.ActionType);
        command.Parameters.AddWithValue("@appName", entry.ApplicationName);
        command.Parameters.AddWithValue("@desc", entry.Description);
        command.Parameters.AddWithValue("@details", entry.Details ?? string.Empty);
        command.Parameters.AddWithValue("@success", entry.Success ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ActivityLogEntry>> GetActivitiesAsync(int limit = 100, string? actionType = null, string? search = null)
    {
        await InitializeAsync();
        var results = new List<ActivityLogEntry>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var query = "SELECT Id, Timestamp, ActionType, ApplicationName, Description, Details, Success FROM ActivityLog WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(actionType) && actionType != "All")
        {
            query += " AND ActionType = @actionType";
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += " AND (ApplicationName LIKE @search OR Description LIKE @search)";
        }
        query += " ORDER BY Id DESC LIMIT @limit";

        using var command = new SqliteCommand(query, connection);
        if (!string.IsNullOrWhiteSpace(actionType) && actionType != "All")
            command.Parameters.AddWithValue("@actionType", actionType);
        if (!string.IsNullOrWhiteSpace(search))
            command.Parameters.AddWithValue("@search", $"%{search}%");
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ActivityLogEntry
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                ActionType = reader.GetString(2),
                ApplicationName = reader.GetString(3),
                Description = reader.GetString(4),
                Details = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Success = reader.GetInt32(6) == 1
            });
        }

        return results;
    }

    public async Task ClearActivitiesAsync()
    {
        await InitializeAsync();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new SqliteCommand("DELETE FROM ActivityLog", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddQuarantineItemAsync(QuarantineItem item)
    {
        await InitializeAsync();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            INSERT INTO Quarantine (Id, OriginalPath, BackupPath, OperationDate, RelatedAppName, ItemType, SizeBytes, ManifestJson, IsRestored)
            VALUES (@id, @orig, @backup, @date, @app, @type, @size, @manifest, @restored);
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", item.Id);
        command.Parameters.AddWithValue("@orig", item.OriginalPath);
        command.Parameters.AddWithValue("@backup", item.BackupPath);
        command.Parameters.AddWithValue("@date", item.OperationDate.ToString("o"));
        command.Parameters.AddWithValue("@app", item.RelatedAppName);
        command.Parameters.AddWithValue("@type", item.ItemType);
        command.Parameters.AddWithValue("@size", item.SizeBytes);
        command.Parameters.AddWithValue("@manifest", item.ManifestJson ?? string.Empty);
        command.Parameters.AddWithValue("@restored", item.IsRestored ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<QuarantineItem>> GetQuarantineItemsAsync(bool includeRestored = false)
    {
        await InitializeAsync();
        var results = new List<QuarantineItem>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string query = "SELECT Id, OriginalPath, BackupPath, OperationDate, RelatedAppName, ItemType, SizeBytes, ManifestJson, IsRestored FROM Quarantine";
        if (!includeRestored)
        {
            query += " WHERE IsRestored = 0";
        }
        query += " ORDER BY OperationDate DESC";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new QuarantineItem
            {
                Id = reader.GetString(0),
                OriginalPath = reader.GetString(1),
                BackupPath = reader.GetString(2),
                OperationDate = DateTime.Parse(reader.GetString(3)),
                RelatedAppName = reader.GetString(4),
                ItemType = reader.GetString(5),
                SizeBytes = reader.GetInt64(6),
                ManifestJson = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                IsRestored = reader.GetInt32(8) == 1
            });
        }

        return results;
    }

    public async Task UpdateQuarantineItemAsync(QuarantineItem item)
    {
        await InitializeAsync();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            UPDATE Quarantine 
            SET IsRestored = @restored, ManifestJson = @manifest
            WHERE Id = @id;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@restored", item.IsRestored ? 1 : 0);
        command.Parameters.AddWithValue("@manifest", item.ManifestJson ?? string.Empty);
        command.Parameters.AddWithValue("@id", item.Id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteQuarantineItemAsync(string id)
    {
        await InitializeAsync();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqliteCommand("DELETE FROM Quarantine WHERE Id = @id", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }
}
