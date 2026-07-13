namespace Soundboard.Core.Audio;

public sealed record AudioDeviceInfo(
    string Id,
    string FriendlyName,
    string DataFlow,
    string State
);

