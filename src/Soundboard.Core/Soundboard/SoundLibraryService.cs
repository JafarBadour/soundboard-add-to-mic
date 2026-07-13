using Soundboard.Core.Storage;

namespace Soundboard.Core.Soundboard;

public sealed class SoundLibraryService
{
    public SoundClip ImportFromFile(string sourcePath, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Path is required.", nameof(sourcePath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Audio file not found.", sourcePath);

        AppStoragePaths.EnsureCreated();

        var id = Guid.NewGuid();
        var clipDir = AppStoragePaths.GetClipDir(id);
        Directory.CreateDirectory(clipDir);

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".audio";

        var destPath = Path.Combine(clipDir, "original" + ext);
        File.Copy(sourcePath, destPath, overwrite: false);

        var clipName = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : name.Trim();

        return new SoundClip(
            Id: id,
            Name: clipName,
            StoredFilePath: destPath,
            Gain: 1.0f,
            Hotkey: null);
    }

    public void DeleteClip(SoundClip clip)
    {
        var clipDir = AppStoragePaths.GetClipDir(clip.Id);
        if (Directory.Exists(clipDir))
            Directory.Delete(clipDir, recursive: true);
    }
}

