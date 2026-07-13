using NAudio.Wave;

namespace Soundboard.Core.Audio;

public interface IClipPlaybackTarget
{
    AudioEngineState State { get; }
    void AddInput(ISampleProvider provider);
    void RemoveInput(ISampleProvider provider);
}
