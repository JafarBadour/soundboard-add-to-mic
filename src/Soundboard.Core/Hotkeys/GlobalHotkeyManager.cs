using System.Runtime.InteropServices;

namespace Soundboard.Core.Hotkeys;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;

    private readonly object _gate = new();
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;
    private IntPtr _hwnd;

    public void AttachWindowHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("HWND is required.", nameof(hwnd));

        lock (_gate)
        {
            _hwnd = hwnd;
        }
    }

    public int Register(Hotkey hotkey, Action action, bool noRepeat = true)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        lock (_gate)
        {
            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("AttachWindowHandle must be called first.");

            var id = _nextId++;
            var mods = hotkey.Modifiers;
            if (noRepeat) mods |= HotkeyModifiers.NoRepeat;

            if (!RegisterHotKey(_hwnd, id, (uint)mods, hotkey.VirtualKey))
                throw new InvalidOperationException($"RegisterHotKey failed (vk={hotkey.VirtualKey}, mods={mods}).");

            _actions[id] = action;
            return id;
        }
    }

    public void Unregister(int id)
    {
        lock (_gate)
        {
            if (_hwnd != IntPtr.Zero)
            {
                try { UnregisterHotKey(_hwnd, id); } catch { /* ignore */ }
            }

            _actions.Remove(id);
        }
    }

    public bool TryHandleWindowMessage(int msg, IntPtr wParam)
    {
        if (msg != WmHotkey)
            return false;

        Action? action;
        lock (_gate)
        {
            _actions.TryGetValue(wParam.ToInt32(), out action);
        }

        if (action is null)
            return true;

        try { action(); } catch { /* swallow to avoid crashing message pump */ }
        return true;
    }

    public void Dispose()
    {
        List<int> ids;
        lock (_gate) { ids = _actions.Keys.ToList(); }
        foreach (var id in ids) Unregister(id);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

