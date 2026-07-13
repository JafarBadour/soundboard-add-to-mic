using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Soundboard.App.Utils;
using Soundboard.App.ViewModels;

namespace Soundboard.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        TrySetWindowIcon();
    }

    private void TrySetWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "soundboard-icon.png");
            if (!File.Exists(iconPath))
                return;

            Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        }
        catch
        {
            // Icon is optional — app should still run without it.
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        WindowChromeHelper.ApplyDarkChrome(this);

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.AddHook(WndProc);
        _vm.AttachWindowHandle(source.Handle);
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _vm.Dispose();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_vm.IsCapturingHotkey)
            return;

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            _vm.CancelHotkeyCapture();
            e.Handled = true;
            return;
        }

        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (HotkeyCaptureHelper.TryCreate(key, System.Windows.Input.Keyboard.Modifiers, out var hotkey))
        {
            _vm.CompleteHotkeyCapture(hotkey);
            e.Handled = true;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        handled = _vm.TryHandleWindowMessage(msg, wParam);
        return IntPtr.Zero;
    }
}
