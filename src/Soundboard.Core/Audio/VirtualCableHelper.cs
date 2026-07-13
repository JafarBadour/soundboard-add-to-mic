namespace Soundboard.Core.Audio;

/// <summary>
/// Identifies virtual audio cable render endpoints (app output) and matching capture endpoints (Discord mic input).
/// </summary>
public static class VirtualCableHelper
{
    private static readonly string[] VirtualRenderKeywords =
    [
        "cable input",
        "vb-audio virtual cable",
        "voicemeeter input",
        "voicemeeter aux input",
        "voicemeeter vaio3 input",
        "virtual audio cable",
        "vac line",
        "soundboard",
        "virtual cable",
    ];

    private static readonly string[] SpeakerKeywords =
    [
        "speaker",
        "headphone",
        "headset",
        "realtek",
        "monitor",
        "display audio",
        "tv",
        "hdmi",
        "digital audio",
    ];

    public static bool IsVirtualCableRenderDevice(string friendlyName)
    {
        var name = friendlyName.ToLowerInvariant();
        return VirtualRenderKeywords.Any(k => name.Contains(k));
    }

    public static bool IsLikelySpeaker(string friendlyName)
    {
        var name = friendlyName.ToLowerInvariant();
        if (IsVirtualCableRenderDevice(friendlyName))
            return false;

        return SpeakerKeywords.Any(k => name.Contains(k));
    }

    /// <summary>
    /// Best render device for routing mixed audio into a virtual mic pipeline.
    /// </summary>
    public static AudioDeviceInfo? FindPreferredRenderDevice(IEnumerable<AudioDeviceInfo> renderDevices)
    {
        var devices = renderDevices.ToList();
        if (devices.Count == 0)
            return null;

        // 1) Our Soundboard driver endpoint
        var soundboard = devices.FirstOrDefault(d =>
            d.FriendlyName.Contains("soundboard", StringComparison.OrdinalIgnoreCase));
        if (soundboard is not null)
            return soundboard;

        // 2) VB-CABLE "CABLE Input" (exact-ish match preferred)
        var cableInput = devices.FirstOrDefault(d =>
            d.FriendlyName.Contains("cable input", StringComparison.OrdinalIgnoreCase));
        if (cableInput is not null)
            return cableInput;

        // 3) Any known virtual cable render endpoint
        return devices.FirstOrDefault(d => IsVirtualCableRenderDevice(d.FriendlyName));
    }

    /// <summary>
    /// Capture device other apps (Discord) should use as microphone.
    /// </summary>
    public static AudioDeviceInfo? FindMatchingCaptureDevice(
        IEnumerable<AudioDeviceInfo> captureDevices,
        AudioDeviceInfo? selectedRender)
    {
        var devices = captureDevices.ToList();
        if (devices.Count == 0)
            return null;

        if (selectedRender is null)
            return devices.FirstOrDefault(d => IsVirtualCableCaptureDevice(d.FriendlyName));

        var renderName = selectedRender.FriendlyName.ToLowerInvariant();

        if (renderName.Contains("cable input"))
            return devices.FirstOrDefault(d =>
                d.FriendlyName.Contains("cable output", StringComparison.OrdinalIgnoreCase));

        if (renderName.Contains("voicemeeter"))
        {
            // VoiceMeeter Input (render) -> VoiceMeeter Output (capture) typically
            return devices.FirstOrDefault(d =>
                d.FriendlyName.Contains("voicemeeter output", StringComparison.OrdinalIgnoreCase))
                ?? devices.FirstOrDefault(d => IsVirtualCableCaptureDevice(d.FriendlyName));
        }

        if (renderName.Contains("soundboard"))
        {
            return devices.FirstOrDefault(d =>
                d.FriendlyName.Contains("soundboard", StringComparison.OrdinalIgnoreCase)
                && IsVirtualCableCaptureDevice(d.FriendlyName))
                ?? devices.FirstOrDefault(d =>
                    d.FriendlyName.Contains("soundboard", StringComparison.OrdinalIgnoreCase));
        }

        return devices.FirstOrDefault(d => IsVirtualCableCaptureDevice(d.FriendlyName));
    }

    public static bool IsVirtualCableCaptureDevice(string friendlyName)
    {
        var name = friendlyName.ToLowerInvariant();
        return name.Contains("cable output")
               || name.Contains("voicemeeter output")
               || name.Contains("vac line")
               || (name.Contains("soundboard") && (name.Contains("output") || name.Contains("mic")))
               || name.Contains("virtual cable");
    }

    public static string GetDiscordSetupHint(AudioDeviceInfo? render, AudioDeviceInfo? capture)
    {
        if (render is null)
            return "No virtual cable found. Install VB-CABLE, then click Refresh devices.";

        if (capture is null)
            return $"In Discord, set Microphone to the matching virtual cable capture device (not '{render.FriendlyName}').";

        return $"In Discord/OBS, set Microphone to: {capture.FriendlyName}";
    }
}
