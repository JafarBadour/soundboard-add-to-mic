namespace Soundboard.Core.Hotkeys;

public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(VirtualKeyToString(VirtualKey));
        return string.Join("+", parts);
    }

    public static bool TryParse(string text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return false;

        var mods = HotkeyModifiers.None;
        uint? key = null;

        foreach (var t in tokens)
        {
            switch (t.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= HotkeyModifiers.Control;
                    break;
                case "alt":
                    mods |= HotkeyModifiers.Alt;
                    break;
                case "shift":
                    mods |= HotkeyModifiers.Shift;
                    break;
                case "win":
                case "windows":
                    mods |= HotkeyModifiers.Win;
                    break;
                default:
                    if (key is not null)
                        return false;
                    if (!TryParseVirtualKey(t, out var vk))
                        return false;
                    key = vk;
                    break;
            }
        }

        if (key is null)
            return false;

        hotkey = new Hotkey(mods, key.Value);
        return true;
    }

    private static bool TryParseVirtualKey(string token, out uint vk)
    {
        vk = 0;

        // Function keys: F1..F24
        if (token.Length is >= 2 and <= 3 && (token[0] is 'f' or 'F'))
        {
            if (int.TryParse(token[1..], out var n) && n is >= 1 and <= 24)
            {
                vk = (uint)(0x70 + (n - 1)); // VK_F1 = 0x70
                return true;
            }
        }

        // Single char keys A-Z / 0-9
        if (token.Length == 1)
        {
            var c = token[0];
            if (c is >= 'a' and <= 'z') c = char.ToUpperInvariant(c);
            if ((c is >= 'A' and <= 'Z') || (c is >= '0' and <= '9'))
            {
                vk = c;
                return true;
            }
        }

        // Minimal named keys
        switch (token.ToLowerInvariant())
        {
            case "space": vk = 0x20; return true;
            case "tab": vk = 0x09; return true;
            case "enter": vk = 0x0D; return true;
            case "escape":
            case "esc": vk = 0x1B; return true;
            case "pause": vk = 0x13; return true;
            case "insert": vk = 0x2D; return true;
            case "delete":
            case "del": vk = 0x2E; return true;
            case "home": vk = 0x24; return true;
            case "end": vk = 0x23; return true;
            case "pageup": vk = 0x21; return true;
            case "pagedown": vk = 0x22; return true;
            case "up": vk = 0x26; return true;
            case "down": vk = 0x28; return true;
            case "left": vk = 0x25; return true;
            case "right": vk = 0x27; return true;
            default: return false;
        }
    }

    private static string VirtualKeyToString(uint vk)
    {
        if (vk is >= 0x70 and <= 0x87) // F1..F24
            return "F" + (vk - 0x70 + 1);

        if ((vk is >= (uint)'A' and <= (uint)'Z') || (vk is >= (uint)'0' and <= (uint)'9'))
            return ((char)vk).ToString();

        return vk switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x13 => "Pause",
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x26 => "Up",
            0x28 => "Down",
            0x25 => "Left",
            0x27 => "Right",
            _ => "VK_" + vk
        };
    }
}

