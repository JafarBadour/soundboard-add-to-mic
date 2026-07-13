using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Soundboard.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(
                e.Exception.Message,
                "Soundboard error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
