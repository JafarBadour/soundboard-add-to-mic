using Microsoft.Data.Sqlite;
using Soundboard.Core.Soundboard;

namespace Soundboard.Core.Persistence;

public sealed class SoundRepository
{
    public IReadOnlyList<SoundClip> GetAll()
    {
        DbInitializer.EnsureCreated();

        using var conn = DbInitializer.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT id, name, stored_path, gain, hotkey
        FROM sounds
        ORDER BY name COLLATE NOCASE;
        """;

        using var reader = cmd.ExecuteReader();
        var list = new List<SoundClip>();
        while (reader.Read())
        {
            var id = Guid.Parse(reader.GetString(0));
            var name = reader.GetString(1);
            var path = reader.GetString(2);
            var gain = (float)reader.GetDouble(3);
            var hotkey = reader.IsDBNull(4) ? null : reader.GetString(4);
            list.Add(new SoundClip(id, name, path, gain, hotkey));
        }

        return list;
    }

    public void Upsert(SoundClip clip)
    {
        DbInitializer.EnsureCreated();

        using var conn = DbInitializer.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO sounds (id, name, stored_path, gain, hotkey)
        VALUES ($id, $name, $path, $gain, $hotkey)
        ON CONFLICT(id) DO UPDATE SET
          name=excluded.name,
          stored_path=excluded.stored_path,
          gain=excluded.gain,
          hotkey=excluded.hotkey;
        """;

        cmd.Parameters.AddWithValue("$id", clip.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$name", clip.Name);
        cmd.Parameters.AddWithValue("$path", clip.StoredFilePath);
        cmd.Parameters.AddWithValue("$gain", clip.Gain);
        cmd.Parameters.AddWithValue("$hotkey", (object?)clip.Hotkey ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void Delete(Guid clipId)
    {
        DbInitializer.EnsureCreated();

        using var conn = DbInitializer.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sounds WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", clipId.ToString("D"));
        cmd.ExecuteNonQuery();
    }
}

