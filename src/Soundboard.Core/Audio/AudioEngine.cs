using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Core.Audio;

public sealed class AudioEngine : IDisposable, IClipPlaybackTarget
{
    private readonly object _gate = new();
    private readonly MMDeviceEnumerator _enumerator = new();

    private WasapiCapture? _micCapture;
    private LowLatencyMicBuffer? _micBuffer;
    private ISampleProvider? _micSampleProvider;

    private MixingSampleProvider? _mixer;
    private IWaveProvider? _mixerWaveProvider;

    private WasapiOut? _output;

    public AudioEngineState State { get; private set; } = AudioEngineState.Stopped;
    public Exception? LastError { get; private set; }

    public WaveFormat? MixWaveFormat { get; private set; }

    public event Action<AudioEngineState>? StateChanged;

    public void Start(AudioEngineConfig config)
    {
        lock (_gate)
        {
            if (State is AudioEngineState.Starting or AudioEngineState.Running)
                return;

            Transition(AudioEngineState.Starting);
            LastError = null;

            try
            {
                StopInternal();

                var inputDevice = _enumerator.GetDevice(config.InputDeviceId);
                _micCapture = new WasapiCapture(inputDevice, useEventSync: true, audioBufferMillisecondsLength: 20)
                {
                    ShareMode = AudioClientShareMode.Shared
                };

                var captureFormat = _micCapture.WaveFormat;
                var sampleRate = config.SampleRate > 0 ? config.SampleRate : captureFormat.SampleRate;
                var channels = config.Channels > 0 ? config.Channels : captureFormat.Channels;

                var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
                MixWaveFormat = mixFormat;

                _micBuffer = new LowLatencyMicBuffer(captureFormat, config.MicBufferMs);
                _micBuffer.Clear();

                _micCapture.DataAvailable += (_, e) =>
                {
                    _micBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };

                _micCapture.RecordingStopped += (_, e) =>
                {
                    if (e.Exception is not null)
                    {
                        lock (_gate)
                        {
                            LastError = e.Exception;
                            Transition(AudioEngineState.Faulted);
                        }
                    }
                };

                _micSampleProvider = MicInputSampleProvider.Create(_micBuffer, mixFormat);

                _mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };
                _mixer.AddMixerInput(_micSampleProvider);

                var clipped = new SoftClipSampleProvider(_mixer);
                _mixerWaveProvider = new SampleToWaveProvider(clipped);

                var outputDevice = _enumerator.GetDevice(config.OutputDeviceId);
                _output = new WasapiOut(
                    outputDevice,
                    AudioClientShareMode.Shared,
                    useEventSync: true,
                    latency: config.DesiredLatencyMs);
                _output.Init(_mixerWaveProvider);

                // Start capture before playback so the mic buffer has data immediately.
                _micCapture.StartRecording();
                _output.Play();

                Transition(AudioEngineState.Running);
            }
            catch (Exception ex)
            {
                LastError = ex;
                Transition(AudioEngineState.Faulted);
                StopInternal();
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

            Transition(AudioEngineState.Stopping);
            StopInternal();
            Transition(AudioEngineState.Stopped);
        }
    }

    public void AddInput(ISampleProvider provider)
    {
        lock (_gate)
        {
            if (_mixer is null || MixWaveFormat is null)
                throw new InvalidOperationException("Engine is not started.");

            var p = provider;
            if (!WaveFormatEquals(p.WaveFormat, MixWaveFormat))
            {
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

        throw new NotSupportedException($"Unsupported channel conversion {source.WaveFormat.Channels} -> {channels}.");
    }

    private void StopInternal()
    {
        try { _micCapture?.StopRecording(); } catch { /* ignore */ }
        try { _output?.Stop(); } catch { /* ignore */ }

        _micCapture?.Dispose();
        _micCapture = null;

        _output?.Dispose();
        _output = null;

        _micBuffer = null;
        _micSampleProvider = null;
        _mixer = null;
        _mixerWaveProvider = null;
        MixWaveFormat = null;
    }

    private void Transition(AudioEngineState next)
    {
        State = next;
        StateChanged?.Invoke(next);
    }

    public void Dispose()
    {
        Stop();
        _enumerator.Dispose();
    }
}
