namespace Soundboard.Core.Audio;

public sealed record AudioEngineConfig(
    string InputDeviceId,
    string OutputDeviceId,
    int SampleRate = 0,
    int Channels = 0,
    int MicBufferMs = 60,
    int DesiredLatencyMs = 20
);

