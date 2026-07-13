using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Soundboard.Core.Audio;

namespace Soundboard.Core.Tests.Audio;

public class MicInputSampleProviderTests
{
    [Fact]
    public void Create_Pcm16Mic_ProducesNonSilentMixOutput()
    {
        var captureFormat = new WaveFormat(48000, 16, 1);
        var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

        var micBuffer = new LowLatencyMicBuffer(captureFormat, maxLatencyMs: 100);
        var tone = GeneratePcm16Tone(captureFormat, 440, frameCount: 4800);
        micBuffer.AddSamples(tone, 0, tone.Length);

        var micSample = MicInputSampleProvider.Create(micBuffer, mixFormat);
        var mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };
        mixer.AddMixerInput(micSample);

        var output = new float[4800];
        var read = mixer.Read(output, 0, output.Length);

        Assert.Equal(output.Length, read);
        Assert.True(Rms(output) > 0.01f, "Expected mic tone in mixer output.");
    }

    [Fact]
    public void Create_FloatMic_ProducesNonSilentMixOutput()
    {
        var captureFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

        var micBuffer = new LowLatencyMicBuffer(captureFormat, maxLatencyMs: 100);
        var tone = GenerateFloatTone(captureFormat, 440, frameCount: 4800);
        micBuffer.AddSamples(tone, 0, tone.Length);

        var micSample = MicInputSampleProvider.Create(micBuffer, mixFormat);
        var mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };
        mixer.AddMixerInput(micSample);

        var output = new float[4800];
        var read = mixer.Read(output, 0, output.Length);

        Assert.Equal(output.Length, read);
        Assert.True(Rms(output) > 0.01f, "Expected mic tone in mixer output.");
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

    private static byte[] GeneratePcm16Tone(WaveFormat format, int frequencyHz, int frameCount)
    {
        var bytes = new byte[frameCount * format.BlockAlign];
        for (var i = 0; i < frameCount; i++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * frequencyHz * i / format.SampleRate) * short.MaxValue * 0.5);
            var offset = i * format.BlockAlign;
            bytes[offset] = (byte)(sample & 0xFF);
            bytes[offset + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return bytes;
    }

    private static byte[] GenerateFloatTone(WaveFormat format, int frequencyHz, int frameCount)
    {
        var bytes = new byte[frameCount * format.BlockAlign];
        for (var i = 0; i < frameCount; i++)
        {
            var sample = (float)(Math.Sin(2 * Math.PI * frequencyHz * i / format.SampleRate) * 0.5);
            var offset = i * format.BlockAlign;
            BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), sample);
        }

        return bytes;
    }
}
