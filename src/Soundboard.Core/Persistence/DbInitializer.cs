using Microsoft.Data.Sqlite;
using Soundboard.Core.Storage;

namespace Soundboard.Core.Persistence;

public static class DbInitializer
{
    public static void EnsureCreated()
    {
        AppStoragePaths.EnsureCreated();

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA journal_mode=WAL;
        PRAGMA foreign_keys=ON;

        CREATE TABLE IF NOT EXISTS sounds (
          id TEXT NOT NULL PRIMARY KEY,
          name TEXT NOT NULL,
          stored_path TEXT NOT NULL,
          gain REAL NOT NULL,
          hotkey TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS settings (
          key TEXT NOT NULL PRIMARY KEY,
          value TEXT NOT NULL
        );
        """;
        cmd.ExecuteNonQuery();
    }

    public static SqliteConnection OpenConnection()
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = AppStoragePaths.DbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        var conn = new SqliteConnection(cs);
        conn.Open();
        return conn;
    }
}

