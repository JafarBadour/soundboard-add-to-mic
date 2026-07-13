namespace Soundboard.Core.Soundboard;

public sealed record SoundClip(
    Guid Id,
    string Name,
    string StoredFilePath,
    float Gain = 1.0f,
    string? Hotkey = null
);

