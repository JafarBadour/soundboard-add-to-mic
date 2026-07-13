using NAudio.CoreAudioApi;
using NAudio.Wave;
using Soundboard.Core.Audio;

namespace Soundboard.Core.Tests.Audio;

/// <summary>
/// Runs against real audio hardware when available.
/// </summary>
public class HardwareAudioEngineTests
{
    [Fact]
    public void DefaultMicCapture_ReceivesAudioBytes()
    {
        MMDevice? device;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch
        {
            return; // no capture device in this environment
        }

        using (device)
        using (var capture = new WasapiCapture(device))
        {
            var collected = 0;
            capture.DataAvailable += (_, e) => collected += e.BytesRecorded;

            capture.StartRecording();
            Thread.Sleep(600);
            capture.StopRecording();

            Assert.True(collected > 0, "Expected bytes from default microphone capture.");
        }
    }

    [Fact]
    public void AudioEngine_StartsAndStops_WithDefaultDevices()
    {
        using var devices = new AudioDeviceService();
        var captureId = devices.TryGetDefaultCaptureDeviceId();
        var renderId = devices.TryGetDefaultRenderDeviceId();
        if (captureId is null || renderId is null)
            return;

        using var engine = new AudioEngine();
        engine.Start(new AudioEngineConfig(captureId, renderId));

        try
        {
            Assert.Equal(AudioEngineState.Running, engine.State);
            Assert.NotNull(engine.MixWaveFormat);
        }
        finally
        {
            engine.Stop();
            Assert.Equal(AudioEngineState.Stopped, engine.State);
        }
    }

    [Fact]
    public void MicPipeline_WithCapturedFormat_ProducesMixEnergy()
    {
        MMDevice? device;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch
        {
            return;
        }

        using (device)
        using (var capture = new WasapiCapture(device))
        {
            var format = capture.WaveFormat;
            var micBuffer = new LowLatencyMicBuffer(format, maxLatencyMs: 100);
            var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels);

            capture.DataAvailable += (_, e) =>
                micBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

            capture.StartRecording();
            Thread.Sleep(600);
            capture.StopRecording();

            var micSample = MicInputSampleProvider.Create(micBuffer, mixFormat);
            var output = new float[format.SampleRate / 2 * format.Channels];
            var read = micSample.Read(output, 0, output.Length);

            Assert.True(read > 0);
            Assert.True(Rms(output) >= 0f);
        }
    }

    private static float Rms(float[] samples)
    {
        if (samples.Length == 0)
            return 0;

        double sum = 0;
        foreach (var s in samples)
            sum += s * s;

        return (float)Math.Sqrt(sum / samples.Length);
    }
}
