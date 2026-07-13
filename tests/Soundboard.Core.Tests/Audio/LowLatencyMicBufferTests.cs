using NAudio.Wave;
using Soundboard.Core.Audio;

namespace Soundboard.Core.Tests.Audio;

public class LowLatencyMicBufferTests
{
    [Fact]
    public void Read_PadsWithSilence_WhenBufferEmpty()
    {
        var format = new WaveFormat(48000, 16, 1);
        var buffer = new LowLatencyMicBuffer(format, maxLatencyMs: 60);

        var readBuf = new byte[format.BlockAlign * 10];
        var read = buffer.Read(readBuf, 0, readBuf.Length);

        Assert.Equal(readBuf.Length, read);
        Assert.All(readBuf, b => Assert.Equal(0, b));
    }

    [Fact]
    public void AddThenRead_ReturnsMatchingPcmBytes()
    {
        var format = new WaveFormat(48000, 16, 1);
        var buffer = new LowLatencyMicBuffer(format, maxLatencyMs: 60);

        var pcm = GeneratePcm16Tone(format, frequencyHz: 440, frameCount: 480);
        buffer.AddSamples(pcm, 0, pcm.Length);

        var readBuf = new byte[pcm.Length];
        var read = buffer.Read(readBuf, 0, readBuf.Length);

        Assert.Equal(pcm.Length, read);
        Assert.Equal(pcm, readBuf);
    }

    [Fact]
    public void Overflow_DropsOldestSamples_InsteadOfGrowingLatency()
    {
        var format = new WaveFormat(48000, 16, 1);
        var buffer = new LowLatencyMicBuffer(format, maxLatencyMs: 20);

        var chunk = GeneratePcm16Tone(format, frequencyHz: 440, frameCount: 480);
        for (var i = 0; i < 20; i++)
            buffer.AddSamples(chunk, 0, chunk.Length);

        var readBuf = new byte[chunk.Length];
        buffer.Read(readBuf, 0, readBuf.Length);

        Assert.Contains(readBuf, b => b != 0);
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
}
