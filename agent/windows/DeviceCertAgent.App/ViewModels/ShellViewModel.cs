using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Services;

namespace DeviceCertAgent.App.ViewModels;

public enum AppPage
{
    Welcome,
    Disclosure,
    Permission,
    ScanProgress,
    ResultPreview,
    Submitting,
    CertificateSuccess,
    UninstallPrompt,
}

public partial class ShellViewModel : ObservableObject
{
    private readonly AgentRuntimeSettings _settings;
    private readonly DiagnosticScanService _scan = new();
    private readonly ScanQualityService _quality = new();
    private readonly LocalCacheCleanupService _cache = new();
    private readonly AgentLogger _logger = new();
    private readonly ScanSessionFlowService? _sessionFlow;
    private readonly MockScanSessionFlowService? _mockFlow;
    private CancellationTokenSource? _scanCts;

    public ShellViewModel(AgentRuntimeSettings settings, AppLaunchOptions launch)
    {
        _settings = settings;
        if (settings.MockApi)
            _mockFlow = new MockScanSessionFlowService();
        else
            _sessionFlow = new ScanSessionFlowService(settings);

        StatusMessage = "Ready when you are.";
        BrandSubtitle = "Trusted device certification for resale and refurbishment";
    }

    [ObservableProperty] private AppPage _currentPage = AppPage.Welcome;
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _submissionStatus = "";
    [ObservableProperty] private bool _adminModeSelected;
    [ObservableProperty] private ScanSummary? _summary;
    [ObservableProperty] private ScanSessionSubmitResponse? _certResult;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _brandSubtitle;

    public bool ShowDeveloperUi => _settings.ShowDeveloperUi;
    public string DeveloperEndpointLabel => _settings.ShowDeveloperUi
        ? $"{_settings.AppEnv} · {_settings.ApiBaseUrl}"
        : "";

    public ObservableCollection<ScanStepProgress> ScanSteps { get; } = [];

    private CollectionResult? _collectionResult;
    private ScanSessionStartResponse? _activeSession;
    private DateTime _scanStartedAt;
    private DateTime _scanCompletedAt;

    [RelayCommand]
    private void BeginCertification() => CurrentPage = AppPage.Disclosure;

    [RelayCommand]
    private void Cancel() => Application.Current.Shutdown();

    [RelayCommand]
    private void ContinueFromDisclosure() => CurrentPage = AppPage.Permission;

    [RelayCommand]
    private void ChooseBasicScan()
    {
        AdminModeSelected = false;
        _ = RunScanAsync();
    }

    [RelayCommand]
    private void ChooseEnhancedScan()
    {
        var dialog = MessageBox.Show(
            "Enhanced scan may request administrator access for SMART storage health, firmware details, and deeper security checks.\n\n" +
            "You can decline and we will continue with a standard scan.",
            "Enhanced administrator scan",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (dialog == MessageBoxResult.Cancel)
            return;

        AdminModeSelected = dialog == MessageBoxResult.Yes;
        if (AdminModeSelected && !EnhancedScanService.IsRunningAsAdmin())
        {
            MessageBox.Show(
                "Administrator access was not granted. Continuing with the standard scan.",
                "VerifyTech Agent",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            AdminModeSelected = false;
        }

        _ = RunScanAsync();
    }

    private async Task RunScanAsync()
    {
        HasError = false;
        ErrorMessage = "";
        CurrentPage = AppPage.ScanProgress;
        ScanSteps.Clear();
        foreach (var step in _scan.CreateSteps())
            ScanSteps.Add(step);

        _scanCts = new CancellationTokenSource();
        var progress = new Progress<ScanStepProgress>(_ =>
        {
            var done = ScanSteps.Count(s => s.Status is ScanStepStatus.Completed or ScanStepStatus.Warning);
            ScanProgress = ScanSteps.Count > 0 ? done * 100.0 / ScanSteps.Count : 0;
            StatusMessage = ScanSteps.FirstOrDefault(s => s.Status == ScanStepStatus.InProgress)?.Title
                ?? "Finalizing scan…";
        });

        try
        {
            _scanStartedAt = DateTime.UtcNow;
            StatusMessage = "Connecting securely to VerifyTech…";

            if (_settings.MockApi)
                _activeSession = await _mockFlow!.StartSessionAsync(_scanCts.Token);
            else
                _activeSession = await _sessionFlow!.StartSessionAsync(_scanCts.Token);

            _collectionResult = await _scan.RunAsync(
                AdminModeSelected,
                progress,
                ScanSteps,
                _scanCts.Token);

            _scanCompletedAt = DateTime.UtcNow;
            ScanProgress = 100;
            StatusMessage = "Scan complete.";

            Summary = _quality.BuildSummary(_collectionResult, AdminModeSelected ? "Enhanced" : "Standard");
            CurrentPage = AppPage.ResultPreview;
        }
        catch (Exception ex)
        {
            _logger.Error("Scan failed", ex);
            ShowError(MapScanError(ex));
            CurrentPage = AppPage.Permission;
        }
    }

    [RelayCommand]
    private async Task SubmitCertification()
    {
        if (_collectionResult is null || _activeSession is null) return;

        if (!_quality.MeetsTier1Minimum(_collectionResult))
        {
            ShowError("We could not collect enough device identity to issue a certificate. Try again on this device.");
            return;
        }

        CurrentPage = AppPage.Submitting;
        SubmissionStatus = "Securing your scan with a one-time session…";
        HasError = false;

        try
        {
            await Task.Delay(300);
            SubmissionStatus = "Creating your trusted certificate…";

            ScanSessionSubmitResponse result;
            if (_settings.MockApi)
            {
                result = await _mockFlow!.SubmitAsync(
                    _activeSession,
                    _collectionResult,
                    _scanStartedAt,
                    _scanCompletedAt,
                    AdminModeSelected,
                    _scanCts?.Token ?? default);
            }
            else
            {
                result = await _sessionFlow!.SubmitAsync(
                    _activeSession,
                    _collectionResult,
                    _scanStartedAt,
                    _scanCompletedAt,
                    AdminModeSelected,
                    _scanCts?.Token ?? default);
            }

            _certResult = result;
            CurrentPage = AppPage.CertificateSuccess;
        }
        catch (Exception ex)
        {
            _logger.Error("Submit failed", ex);
            ShowError(MapScanError(ex));
            CurrentPage = AppPage.ResultPreview;
        }
    }

    [RelayCommand]
    private void CopyCertificateCode()
    {
        if (_certResult?.CertificateCode is not null)
            Clipboard.SetText(_certResult.CertificateCode);
    }

    [RelayCommand]
    private void CopyReportLink()
    {
        if (_certResult?.ReportUrl is not null)
            Clipboard.SetText(_certResult.ReportUrl);
    }

    [RelayCommand]
    private void OpenReport()
    {
        if (_certResult?.ReportUrl is not null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_certResult.ReportUrl)
            {
                UseShellExecute = true,
            });
    }

    [RelayCommand]
    private void FinishCertificate() => CurrentPage = AppPage.UninstallPrompt;

    [RelayCommand]
    private void ClearLocalCache()
    {
        _cache.ClearCache();
        MessageBox.Show("Local scan cache removed.", "VerifyTech Agent", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void KeepInstalled() => Application.Current.Shutdown();

    [RelayCommand]
    private void ExitApp() => Application.Current.Shutdown();

    [RelayCommand]
    private void BackToWelcome()
    {
        HasError = false;
        CurrentPage = AppPage.Welcome;
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        MessageBox.Show(message, "VerifyTech Agent", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string MapScanError(Exception ex) => ex switch
    {
        InvalidOperationException ioe => ioe.Message,
        HttpRequestException => "Unable to reach VerifyTech. Check your internet connection and try again.",
        UnauthorizedAccessException => "Permission was denied for part of this scan. You can retry with a standard scan.",
        _ => "Something went wrong during the scan. Please try again.",
    };
}
