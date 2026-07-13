namespace Soundboard.Core.Storage;

public static class AppStoragePaths
{
    public static string RootDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Soundboard");

    public static string ClipsDir => Path.Combine(RootDir, "clips");

    public static string DbPath => Path.Combine(RootDir, "soundboard.sqlite");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(ClipsDir);
    }

    public static string GetClipDir(Guid clipId) => Path.Combine(ClipsDir, clipId.ToString("N"));
}

