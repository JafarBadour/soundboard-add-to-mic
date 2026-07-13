using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Core.Soundboard;

/// <summary>
/// Reads a decoded clip file and emits steady full blocks for the mixer (handles MP3/Media Foundation priming).
/// </summary>
public sealed class ClipFileSampleProvider : ISampleProvider, IDisposable
{
    private readonly AudioFileReader _reader;
    private readonly VolumeSampleProvider _volume;
    private bool _finished;

    public ClipFileSampleProvider(string filePath, float gain)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio clip not found.", filePath);

        _reader = new AudioFileReader(filePath);
        _volume = new VolumeSampleProvider(_reader)
        {
            Volume = Math.Clamp(gain, 0f, 4f)
        };
        WaveFormat = _volume.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_finished)
            return 0;

        var read = _volume.Read(buffer, offset, count);
        if (read > 0)
        {
            if (read < count)
                Array.Clear(buffer, offset + read, count - read);

            return count;
        }

        // MP3/Media Foundation can briefly return 0 before the first PCM block is ready.
        if (_reader.Length > 0 && _reader.Position < _reader.Length)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        _finished = true;
        return 0;
    }

    public void Dispose() => _reader.Dispose();
}
