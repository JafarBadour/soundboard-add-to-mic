namespace Soundboard.Core.Persistence;

public sealed class SettingsRepository
{
    public string? Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));

        DbInitializer.EnsureCreated();

        using var conn = DbInitializer.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        var result = cmd.ExecuteScalar();
        return result is null || result is DBNull ? null : (string)result;
    }

    public void Set(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));

        DbInitializer.EnsureCreated();

        using var conn = DbInitializer.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO settings (key, value)
        VALUES ($key, $value)
        ON CONFLICT(key) DO UPDATE SET value=excluded.value;
        """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
}

