using System.Windows;
using System.Windows.Threading;
using DeviceCertAgent.App.Services;
using DeviceCertAgent.App.ViewModels;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Services;

namespace DeviceCertAgent.App;

public partial class App : Application
{
    private readonly AgentLogger _logger = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        if (!SingleInstanceService.TryAcquire(e.Args))
        {
            Shutdown();
            return;
        }

        var (settings, launch) = new SecureEndpointResolver().Resolve(e.Args);
        var shell = new ShellViewModel(settings, launch);
        var window = new MainWindow { DataContext = shell };
        MainWindow = window;
        window.Loaded += (_, _) =>
        {
            shell.BeginEnhancedScanIfRequested();
            shell.BeginPairingIfRequested();
        };
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            "Something unexpected happened, but VerifyTech Agent will keep running.\n\n" +
            "If you were testing the camera, tap Retry camera or mark the test as failed.",
            "VerifyTech Agent",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _logger.Error("Unhandled domain exception", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error("Unobserved task exception", e.Exception);
        e.SetObserved();
    }
}
