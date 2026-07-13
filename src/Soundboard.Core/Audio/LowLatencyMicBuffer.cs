using NAudio.Wave;

namespace Soundboard.Core.Audio;

/// <summary>
/// Fixed-size ring buffer for live mic capture. Drops oldest audio when full to cap latency.
/// </summary>
public sealed class LowLatencyMicBuffer : IWaveProvider
{
    private readonly byte[] _ring;
    private readonly int _capacity;
    private readonly object _sync = new();
    private int _write;
    private int _read;
    private int _count;

    public LowLatencyMicBuffer(WaveFormat format, int maxLatencyMs = 60)
    {
        WaveFormat = format;
        var bytesPerMs = Math.Max(format.AverageBytesPerSecond / 1000, format.BlockAlign);
        _capacity = Math.Max(format.BlockAlign * 16, bytesPerMs * maxLatencyMs);
        _ring = new byte[_capacity];
    }

    public WaveFormat WaveFormat { get; }

    public void AddSamples(byte[] buffer, int offset, int bytesRecorded)
    {
        if (bytesRecorded <= 0)
            return;

        lock (_sync)
        {
            DropOldestIfNeeded(bytesRecorded);
            WriteBytes(buffer, offset, bytesRecorded);
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        count -= count % WaveFormat.BlockAlign;
        if (count <= 0)
            return 0;

        lock (_sync)
        {
            var toRead = Math.Min(count, _count);
            if (toRead > 0)
                ReadBytes(buffer, offset, toRead);

            // Pad with silence so downstream (WasapiOut) keeps a steady clock.
            if (toRead < count)
                Array.Clear(buffer, offset + toRead, count - toRead);

            return count;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _write = 0;
            _read = 0;
            _count = 0;
        }
    }

    private void DropOldestIfNeeded(int incoming)
    {
        while (_count + incoming > _capacity)
        {
            var drop = Math.Min(WaveFormat.BlockAlign * 8, _count);
            if (drop <= 0)
                break;

            _read = (_read + drop) % _capacity;
            _count -= drop;
        }
    }

    private void WriteBytes(byte[] buffer, int offset, int bytesRecorded)
    {
        var remaining = bytesRecorded;
        var src = offset;

        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, _capacity - _write);
            Buffer.BlockCopy(buffer, src, _ring, _write, chunk);
            _write = (_write + chunk) % _capacity;
            src += chunk;
            remaining -= chunk;
            _count += chunk;
        }
    }

    private void ReadBytes(byte[] buffer, int offset, int toRead)
    {
        var remaining = toRead;
        var dst = offset;

        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, _capacity - _read);
            Buffer.BlockCopy(_ring, _read, buffer, dst, chunk);
            _read = (_read + chunk) % _capacity;
            dst += chunk;
            remaining -= chunk;
            _count -= chunk;
        }
    }
}
