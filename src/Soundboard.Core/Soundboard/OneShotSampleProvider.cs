using NAudio.Wave;

namespace Soundboard.Core.Soundboard;

public sealed class OneShotSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private readonly Action? _onCompleted;
    private readonly IDisposable? _toDispose;

    private int _completed;

    public OneShotSampleProvider(ISampleProvider source, IDisposable? toDispose = null, Action? onCompleted = null)
    {
        _source = source;
        _toDispose = toDispose;
        _onCompleted = onCompleted;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_completed != 0)
            return 0;

        var read = _source.Read(buffer, offset, count);
        if (read > 0)
            return read;

        CompleteOnce();
        return 0;
    }

    private void CompleteOnce()
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        try { _onCompleted?.Invoke(); } catch { /* ignore */ }
        try { _toDispose?.Dispose(); } catch { /* ignore */ }
    }

    public void Dispose() => CompleteOnce();
}

