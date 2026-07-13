using Soundboard.Core.Audio;

namespace Soundboard.Core.Soundboard;

public sealed class SoundPlayerService
{
    private readonly IClipPlaybackTarget _main;
    private readonly IClipPlaybackTarget? _monitor;

    public SoundPlayerService(IClipPlaybackTarget main, IClipPlaybackTarget? monitor = null)
    {
        _main = main;
        _monitor = monitor;
    }

    public void Play(SoundClip clip)
    {
        if (_main.State != AudioEngineState.Running)
            throw new InvalidOperationException("Start the audio engine before playing sounds.");

        PlayToTarget(_main, clip);

        if (_monitor is not null && _monitor.State == AudioEngineState.Running)
            PlayToTarget(_monitor, clip);
    }

    private static void PlayToTarget(IClipPlaybackTarget target, SoundClip clip)
    {
        var clipProvider = new ClipFileSampleProvider(clip.StoredFilePath, clip.Gain);

        OneShotSampleProvider? oneShot = null;
        oneShot = new OneShotSampleProvider(
            clipProvider,
            toDispose: clipProvider,
            onCompleted: () =>
            {
                if (oneShot is not null)
                    target.RemoveInput(oneShot);
            });

        target.AddInput(oneShot);
    }
}
