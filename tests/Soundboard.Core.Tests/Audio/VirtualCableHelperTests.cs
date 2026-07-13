using Soundboard.Core.Audio;

namespace Soundboard.Core.Tests.Audio;

public class VirtualCableHelperTests
{
    [Fact]
    public void FindPreferredRenderDevice_PrefersCableInputOverSpeakers()
    {
        var devices = new[]
        {
            new AudioDeviceInfo("1", "Speakers (Realtek)", "Render", "Active"),
            new AudioDeviceInfo("2", "CABLE Input (VB-Audio Virtual Cable)", "Render", "Active"),
        };

        var preferred = VirtualCableHelper.FindPreferredRenderDevice(devices);

        Assert.NotNull(preferred);
        Assert.Equal("2", preferred!.Id);
    }

    [Fact]
    public void FindMatchingCaptureDevice_MapsCableInputToCableOutput()
    {
        var render = new AudioDeviceInfo("2", "CABLE Input (VB-Audio Virtual Cable)", "Render", "Active");
        var captureDevices = new[]
        {
            new AudioDeviceInfo("a", "Microphone (USB)", "Capture", "Active"),
            new AudioDeviceInfo("b", "CABLE Output (VB-Audio Virtual Cable)", "Capture", "Active"),
        };

        var capture = VirtualCableHelper.FindMatchingCaptureDevice(captureDevices, render);

        Assert.NotNull(capture);
        Assert.Equal("b", capture!.Id);
    }
}
