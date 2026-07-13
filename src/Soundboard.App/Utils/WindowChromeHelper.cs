using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Soundboard.App.Utils;

internal static class WindowChromeHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaBorderColor = 34;

    // #0F1419 — same as BgBrush in App.xaml (stored as BGR for DWM)
    private const int AppBackgroundBgr = 0x191410;

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    public static void ApplyDarkChrome(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero)
            return;

        EnableDarkMode(hwnd);
        SetCaptionColor(hwnd, AppBackgroundBgr);
        SetBorderColor(hwnd, AppBackgroundBgr);
        SetTextColor(hwnd, 0xF1ECE8); // light text BGR
        ExtendFrameIntoClientArea(hwnd);
    }

    private static void ExtendFrameIntoClientArea(nint hwnd)
    {
        // Pulls the DWM frame into the client area so the outer edge matches our background.
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private static void EnableDarkMode(nint hwnd)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            return;

        var value = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private static void SetCaptionColor(nint hwnd, int colorBgr)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref colorBgr, sizeof(int));
    }

    private static void SetBorderColor(nint hwnd, int colorBgr)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref colorBgr, sizeof(int));
    }

    private static void SetTextColor(nint hwnd, int colorBgr)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref colorBgr, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref Margins margins);
}
