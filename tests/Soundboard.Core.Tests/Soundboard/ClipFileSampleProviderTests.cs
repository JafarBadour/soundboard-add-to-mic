using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Soundboard.Core.Soundboard;

namespace Soundboard.Core.Tests.Soundboard;

public class ClipFileSampleProviderTests
{
    [Fact]
    public void WavClip_ProducesNonSilentMixerOutput()
    {
        var wavPath = CreateTempWav();
        try
        {
            AssertClipMixesIntoOutput(wavPath);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    [Fact]
    public void Mp3Clip_ProducesNonSilentMixerOutput_WhenMediaFoundationAvailable()
    {
        var wavPath = CreateTempWav();
        var mp3Path = Path.ChangeExtension(wavPath, ".mp3");

        try
        {
            if (!TryConvertWavToMp3(wavPath, mp3Path))
                return; // ffmpeg not installed; skip mp3-specific check

            AssertClipMixesIntoOutput(mp3Path);
        }
        finally
        {
            if (File.Exists(wavPath)) File.Delete(wavPath);
            if (File.Exists(mp3Path)) File.Delete(mp3Path);
        }
    }

    private static void AssertClipMixesIntoOutput(string clipPath)
    {
        var mixFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        using var clip = new ClipFileSampleProvider(clipPath, gain: 1f);

        ISampleProvider clipSample = clip;
        if (clip.WaveFormat.SampleRate != mixFormat.SampleRate)
            clipSample = new WdlResamplingSampleProvider(clipSample, mixFormat.SampleRate);
        if (clip.WaveFormat.Channels == 1 && mixFormat.Channels == 2)
            clipSample = new MonoToStereoSampleProvider(clipSample);

        var mixer = new MixingSampleProvider(mixFormat) { ReadFully = true };
        mixer.AddMixerInput(clipSample);

        var output = new float[mixFormat.SampleRate / 2 * mixFormat.Channels];
        var read = mixer.Read(output, 0, output.Length);

        Assert.Equal(output.Length, read);
        Assert.True(Rms(output) > 0.01f, $"Expected audible output from clip: {clipPath}");
    }

    private static string CreateTempWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundboard-test-{Guid.NewGuid():N}.wav");
        var format = new WaveFormat(44100, 16, 1);
        using (var writer = new WaveFileWriter(path, format))
        {
            var frameCount = format.SampleRate;
            var buffer = new byte[frameCount * format.BlockAlign];
            for (var i = 0; i < frameCount; i++)
            {
                var sample = (short)(Math.Sin(2 * Math.PI * 440 * i / format.SampleRate) * short.MaxValue * 0.8);
                var offset = i * format.BlockAlign;
                buffer[offset] = (byte)(sample & 0xFF);
                buffer[offset + 1] = (byte)((sample >> 8) & 0xFF);
            }

            writer.Write(buffer, 0, buffer.Length);
        }

        return path;
    }

    private static bool TryConvertWavToMp3(string wavPath, string mp3Path)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{wavPath}\" -codec:a libmp3lame -qscale:a 5 \"{mp3Path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return false;

            process.WaitForExit(15000);
            return process.ExitCode == 0 && File.Exists(mp3Path);
        }
        catch
        {
            return false;
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
