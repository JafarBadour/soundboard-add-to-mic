using System.Windows.Input;
using Soundboard.Core.Hotkeys;

namespace Soundboard.App.Utils;

public static class HotkeyCaptureHelper
{
    public static bool TryCreate(Key key, ModifierKeys modifiers, out Hotkey hotkey)
    {
        hotkey = default;

        if (IsModifierKey(key))
            return false;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk <= 0)
            return false;

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
            mods |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt))
            mods |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift))
            mods |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows))
            mods |= HotkeyModifiers.Win;

        hotkey = new Hotkey(mods, (uint)vk);
        return true;
    }

    private static bool IsModifierKey(Key key)
        => key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
}
