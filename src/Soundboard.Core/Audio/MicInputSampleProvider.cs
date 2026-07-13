using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Core.Audio;

/// <summary>
/// Converts live mic bytes (PCM16 or IEEE float) into float samples for mixing.
/// </summary>
public static class MicInputSampleProvider
{
    public static ISampleProvider Create(IWaveProvider micWaveProvider, WaveFormat targetMixFormat)
    {
        ISampleProvider micSample = micWaveProvider.WaveFormat.Encoding switch
        {
            WaveFormatEncoding.IeeeFloat => new WaveToSampleProvider(micWaveProvider),
            WaveFormatEncoding.Pcm when micWaveProvider.WaveFormat.BitsPerSample == 16
                => new Pcm16BitToSampleProvider(micWaveProvider),
            WaveFormatEncoding.Pcm when micWaveProvider.WaveFormat.BitsPerSample == 24
                => new Pcm24BitToSampleProvider(micWaveProvider),
            WaveFormatEncoding.Pcm when micWaveProvider.WaveFormat.BitsPerSample == 32
                => new Pcm32BitToSampleProvider(micWaveProvider),
            _ => throw new NotSupportedException(
                $"Unsupported mic format: {micWaveProvider.WaveFormat}")
        };

        if (!WaveFormatEquals(micSample.WaveFormat, targetMixFormat))
        {
            if (micSample.WaveFormat.SampleRate != targetMixFormat.SampleRate)
                micSample = new WdlResamplingSampleProvider(micSample, targetMixFormat.SampleRate);

            micSample = EnsureChannelCount(micSample, targetMixFormat.Channels);
        }

        return micSample;
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
}
