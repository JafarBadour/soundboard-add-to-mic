using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Core.Audio;

/// <summary>
/// Plays soundboard clips to a local output device (speakers/headphones) for monitoring.
/// Does not include microphone passthrough.
/// </summary>
public sealed class ClipMonitorEngine : IDisposable, IClipPlaybackTarget
{
    private readonly object _gate = new();
    private readonly MMDeviceEnumerator _enumerator = new();

    private MixingSampleProvider? _mixer;
    private IWaveProvider? _mixerWaveProvider;
    private WasapiOut? _output;

    public AudioEngineState State { get; private set; } = AudioEngineState.Stopped;
    public WaveFormat? MixWaveFormat { get; private set; }

    public void Start(string outputDeviceId, WaveFormat mixFormat, int desiredLatencyMs = 20)
    {
        lock (_gate)
        {
            if (State is AudioEngineState.Starting or AudioEngineState.Running)
                return;

            State = AudioEngineState.Starting;

            try
            {
                StopInternal();

                MixWaveFormat = mixFormat;
                _mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };

                var clipped = new SoftClipSampleProvider(_mixer);
                _mixerWaveProvider = new SampleToWaveProvider(clipped);

                var outputDevice = _enumerator.GetDevice(outputDeviceId);
                _output = new WasapiOut(
                    outputDevice,
                    AudioClientShareMode.Shared,
                    useEventSync: true,
                    latency: desiredLatencyMs);
                _output.Init(_mixerWaveProvider);
                _output.Play();

                State = AudioEngineState.Running;
            }
            catch
            {
                StopInternal();
                State = AudioEngineState.Faulted;
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (State is AudioEngineState.Stopping or AudioEngineState.Stopped)
                return;

            State = AudioEngineState.Stopping;
            StopInternal();
            State = AudioEngineState.Stopped;
        }
    }

    public void AddInput(ISampleProvider provider)
    {
        lock (_gate)
        {
            if (_mixer is null || MixWaveFormat is null)
                throw new InvalidOperationException("Monitor engine is not started.");

            var p = provider;
            if (!WaveFormatEquals(p.WaveFormat, MixWaveFormat))
            {
                if (p.WaveFormat.SampleRate != MixWaveFormat.SampleRate)
                    p = new WdlResamplingSampleProvider(p, MixWaveFormat.SampleRate);

                p = EnsureChannelCount(p, MixWaveFormat.Channels);
            }

            _mixer.AddMixerInput(p);
        }
    }

    public void RemoveInput(ISampleProvider provider)
    {
        lock (_gate)
        {
            _mixer?.RemoveMixerInput(provider);
        }
    }

    private static bool WaveFormatEquals(WaveFormat a, WaveFormat b)
        => a.SampleRate == b.SampleRate
           && a.Channels == b.Channels
           && a.Encoding == b.Encoding;

    private static ISampleProvider EnsureChannelCount(ISampleProvider source, int channels)
    {
        if (source.WaveFormat.Channels == channels)
            return source;

        if (source.WaveFormat.Channels == 1 && channels == 2)
            return new MonoToStereoSampleProvider(source);

        if (source.WaveFormat.Channels == 2 && channels == 1)
            return new StereoToMonoSampleProvider(source);

        throw new NotSupportedException(
            $"Unsupported channel conversion {source.WaveFormat.Channels} -> {channels}.");
    }

    private void StopInternal()
    {
        try { _output?.Stop(); } catch { /* ignore */ }

        _output?.Dispose();
        _output = null;
        _mixer = null;
        _mixerWaveProvider = null;
        MixWaveFormat = null;
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }
}
