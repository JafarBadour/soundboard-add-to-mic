using NAudio.Wave;

namespace Soundboard.Core.Audio;

/// <summary>
/// A simple soft clipper to prevent harsh digital clipping.
/// </summary>
public sealed class SoftClipSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _drive;

    public SoftClipSampleProvider(ISampleProvider source, float drive = 1.5f)
    {
        _source = source;
        _drive = drive <= 0 ? 1 : drive;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var n = 0; n < read; n++)
        {
            var i = offset + n;
            var x = buffer[i] * _drive;
            // tanh-like soft clip, cheap and stable
            buffer[i] = x / (1f + MathF.Abs(x));
        }

        return read;
    }
}

