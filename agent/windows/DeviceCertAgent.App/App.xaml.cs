using System.Windows;
using DeviceCertAgent.App.ViewModels;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new EndpointConfigurationService();
        var (settings, launch) = config.Resolve(e.Args);

        var shell = new ShellViewModel(settings, launch);
        var window = new MainWindow { DataContext = shell };
        MainWindow = window;
        window.Show();

        if (launch.Mode == "verify" && !string.IsNullOrWhiteSpace(launch.CertificateCode))
        {
            shell.CertificateCode = launch.CertificateCode;
            shell.SelectedMode = "verify";
        }
    }
}
