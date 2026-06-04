using System.Windows;
using DeviceCertAgent.App.ViewModels;
using DeviceCertAgent.Core.Configuration;

namespace DeviceCertAgent.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var (settings, _) = new SecureEndpointResolver().Resolve(e.Args);
        var shell = new ShellViewModel(settings, new());
        var window = new MainWindow { DataContext = shell };
        MainWindow = window;
        window.Show();
    }
}
