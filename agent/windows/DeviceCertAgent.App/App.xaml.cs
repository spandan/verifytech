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
            MessageBox.Show(
                "Certronx Agent is already running.\n\nCheck your taskbar or press Alt+Tab to find the open window.",
                AppBranding.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var (settings, launch) = new SecureEndpointResolver().Resolve(e.Args);
        ProtocolRegistrationService.EnsureRegistered();
        var shell = new ShellViewModel(settings, launch);
        Window window;
        try
        {
            window = new MainWindow { DataContext = shell };
        }
        catch (Exception ex)
        {
            var detail = ex.Message;
            if (ex is System.Windows.Markup.XamlParseException xpe)
                detail = $"{xpe.Message} (line {xpe.LineNumber}, pos {xpe.LinePosition})";
            _logger.Error($"Failed to load agent window: {detail}", ex);
            MessageBox.Show(
                "Certronx Agent could not open its window.\n\n" +
                "Try closing any running copy from Task Manager, then run Launch-CertronxAgent.cmd again.\n\n" +
                $"Details: {detail}",
                AppBranding.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        MainWindow = window;
        window.Loaded += (_, _) =>
        {
            shell.InitializeLaunchFlow();
            shell.BeginEnhancedScanIfRequested();
        };
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            "Something unexpected happened, but Certronx Agent will keep running.\n\n" +
            "If you were testing the camera, tap Retry camera or mark the test as failed.",
            AppBranding.AppName,
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
