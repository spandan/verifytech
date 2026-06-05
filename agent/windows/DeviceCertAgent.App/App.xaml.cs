using System.Windows;
using DeviceCertAgent.App.ViewModels;
using DeviceCertAgent.Core.Configuration;

namespace DeviceCertAgent.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var (settings, launch) = new SecureEndpointResolver().Resolve(e.Args);
        var shell = new ShellViewModel(settings, launch);
        var window = new MainWindow { DataContext = shell };
        MainWindow = window;
        window.Loaded += (_, _) => shell.BeginEnhancedScanIfRequested();
        window.Show();
    }
}
