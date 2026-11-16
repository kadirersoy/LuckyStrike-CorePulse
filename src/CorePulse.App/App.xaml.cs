using System.Windows;

namespace CorePulse.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"CorePulse Çalışma Hatası: {args.Exception.Message}\n\nDetay:\n{args.Exception.StackTrace}", "CorePulse Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Kritik Hata: {ex.Message}\n\n{ex.StackTrace}", "CorePulse Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }
}

