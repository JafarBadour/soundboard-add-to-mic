using NAudio.CoreAudioApi;

namespace Soundboard.Core.Audio;

public sealed class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<AudioDeviceInfo> ListCaptureDevices()
        => ListDevices(DataFlow.Capture);

    public IReadOnlyList<AudioDeviceInfo> ListRenderDevices()
        => ListDevices(DataFlow.Render);

    public MMDevice GetDeviceById(string id) => _enumerator.GetDevice(id);

    public string? TryGetDefaultCaptureDeviceId()
        => TryGetDefaultDeviceId(DataFlow.Capture);

    public string? TryGetDefaultRenderDeviceId()
        => TryGetDefaultDeviceId(DataFlow.Render);

    private IReadOnlyList<AudioDeviceInfo> ListDevices(DataFlow flow)
    {
        var devices = _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
        return devices
            .Select(d => new AudioDeviceInfo(
                d.ID,
                d.FriendlyName,
                flow.ToString(),
                d.State.ToString()))
            .ToList();
    }

    private string? TryGetDefaultDeviceId(DataFlow flow)
    {
        try
        {
            var dev = _enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return dev?.ID;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _enumerator.Dispose();
}

