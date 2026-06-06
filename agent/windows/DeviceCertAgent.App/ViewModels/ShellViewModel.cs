using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCertAgent.App.Services;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Services;
using DeviceCertAgent.Core.Services.V2;

namespace DeviceCertAgent.App.ViewModels;

public enum AppPage
{
    Welcome,
    PairingManual,
    PairingConnecting,
    Disclosure,
    Permission,
    ScanProgress,
    FunctionalTests,
    ResultPreview,
    Submitting,
    CertificateSuccess,
    UninstallPrompt,
}

public partial class ShellViewModel : ObservableObject
{
    private readonly AgentRuntimeSettings _settings;
    private readonly DiagnosticScanService _scan = new();
    private readonly DeepCertificationOrchestrator _deepCert = new();
    public FunctionalTestsViewModel FunctionalTests { get; } = new();
    private readonly ScanQualityService _quality = new();
    private readonly LocalCacheCleanupService _cache = new();
    private readonly AgentLogger _logger = new();
    private readonly ScanSessionFlowService? _sessionFlow;
    private readonly MockScanSessionFlowService? _mockFlow;
    private readonly AppLaunchOptions _launch;
    private CancellationTokenSource? _scanCts;

    public ShellViewModel(AgentRuntimeSettings settings, AppLaunchOptions launch)
    {
        _settings = settings;
        _launch = launch;
        if (settings.MockApi)
            _mockFlow = new MockScanSessionFlowService();
        else
            _sessionFlow = new ScanSessionFlowService(settings);

        StatusMessage = "Ready when you are.";
        BrandSubtitle = "Trusted device certification for resale and refurbishment";
        FunctionalTests.ReachedCompleteStep += OnFunctionalTestsReachedComplete;
    }

    [ObservableProperty] private AppPage _currentPage = AppPage.Welcome;
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _submissionStatus = "";
    [ObservableProperty] private bool _adminModeSelected;
    [ObservableProperty] private ScanSummary? _summary;
    [ObservableProperty] private string _inspectionReport = "";
    [ObservableProperty] private CertificationSummaryReport? _inspectionReportSummary;
    [ObservableProperty] private ScanSessionSubmitResponse? _certResult;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _brandSubtitle;
    [ObservableProperty] private string _manualPairingCode = "";
    [ObservableProperty] private string _linkedAccountName = "";
    [ObservableProperty] private bool _isPreparingReport;
    [ObservableProperty] private bool _isReportReady;
    [ObservableProperty] private string _reportStatusMessage = "";
    [ObservableProperty] private bool _reportPreparationFailed;

    public ObservableCollection<ScanStepProgress> ScanSteps { get; } = [];

    private CollectionResult? _collectionResult;
    private ScanSessionStartResponse? _activeSession;
    private PairedScanContext? _pairedContext;
    private bool _isPairedFlow;
    private DateTime _scanStartedAt;
    private DateTime _scanCompletedAt;
    private Task? _prepareReportTask;

    [RelayCommand]
    private void BeginCertification() => CurrentPage = AppPage.Disclosure;

    [RelayCommand]
    private void OpenManualPairing()
    {
        HasError = false;
        CurrentPage = AppPage.PairingManual;
    }

    [RelayCommand]
    private void SubmitManualPairingCode()
    {
        if (string.IsNullOrWhiteSpace(ManualPairingCode))
        {
            ShowError("Enter the pairing code from the Certronx website.");
            return;
        }

        _ = BeginPairedFlowAsync(ManualPairingCode.Trim());
    }

    public void BeginPairingIfRequested()
    {
        if (string.IsNullOrWhiteSpace(_launch.PairingCode))
            return;
        _ = BeginPairedFlowAsync(_launch.PairingCode.Trim());
    }

    public void HandleForwardedPairingCode(string pairingCode)
    {
        if (string.IsNullOrWhiteSpace(pairingCode))
            return;
        _ = BeginPairedFlowAsync(pairingCode.Trim());
    }

    private async Task BeginPairedFlowAsync(string pairingCode)
    {
        if (_settings.MockApi)
        {
            ShowError("Paired scan is not available in mock mode.");
            CurrentPage = AppPage.Welcome;
            return;
        }

        HasError = false;
        ErrorMessage = "";
        CurrentPage = AppPage.PairingConnecting;
        StatusMessage = "Connecting to Certronx...";
        AdminModeSelected = false;
        _isPairedFlow = true;
        _pairedContext = null;

        try
        {
            var bootstrap = await Task.Run(() => HardwareFingerprintService.CollectPairingBootstrap(out _));
            var fingerprint = HardwareFingerprintService.Compute(bootstrap);
            var exchange = await _sessionFlow!.ExchangePairingAsync(pairingCode, fingerprint);
            _pairedContext = new PairedScanContext
            {
                UploadToken = exchange.UploadToken,
                ScanSessionId = exchange.ScanSessionId,
                LinkedAccountName = exchange.LinkedAccountName,
            };
            _activeSession = new ScanSessionStartResponse
            {
                SessionId = exchange.ScanSessionId,
                Nonce = "",
            };
            LinkedAccountName = exchange.LinkedAccountName ?? "your Certronx account";
            StatusMessage = $"Linked to {LinkedAccountName}. Starting scan…";
            await Task.Delay(500);
            await RunScanAsync(skipSessionStart: true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Pairing failed (api={_settings.ApiBaseUrl})", ex);
            _isPairedFlow = false;
            ShowError(MapScanError(ex));
            ManualPairingCode = pairingCode;
            CurrentPage = AppPage.PairingManual;
        }
    }

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
            "Enhanced scan uses administrator access for SMART storage health, firmware details, and deeper security checks.\n\n" +
            "VerifyTech will restart with administrator privileges.",
            "Enhanced administrator scan",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (dialog == MessageBoxResult.Cancel)
            return;

        if (dialog == MessageBoxResult.No)
        {
            AdminModeSelected = false;
            _ = RunScanAsync();
            return;
        }

        if (!EnhancedScanService.IsRunningAsAdmin())
        {
            if (AdminElevationService.TryRelaunchAsAdmin(["--enhanced-scan"]))
            {
                Application.Current.Shutdown();
                return;
            }

            MessageBox.Show(
                "Administrator access was not granted. You can run a basic scan instead.",
                "VerifyTech Agent",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        AdminModeSelected = true;
        _ = RunScanAsync();
    }

    public void BeginEnhancedScanIfRequested()
    {
        if (!_launch.EnhancedScanOnStartup)
            return;

        if (!EnhancedScanService.IsRunningAsAdmin())
        {
            MessageBox.Show(
                "Enhanced scan requires administrator mode. Please choose Enhanced scan again.",
                "VerifyTech Agent",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            CurrentPage = AppPage.Permission;
            return;
        }

        AdminModeSelected = true;
        CurrentPage = AppPage.Permission;
        _ = RunScanAsync();
    }

    private async Task RunScanAsync(bool skipSessionStart = false)
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
                ?? (_isPairedFlow ? "Scanning this device…" : "Finalizing scan…");
        });

        try
        {
            _scanStartedAt = DateTime.UtcNow;
            if (!skipSessionStart)
            {
                StatusMessage = "Connecting securely to VerifyTech…";

                if (_settings.MockApi)
                    _activeSession = await _mockFlow!.StartSessionAsync(_scanCts.Token);
                else
                    _activeSession = await _sessionFlow!.StartSessionAsync(_scanCts.Token);
            }
            else if (_activeSession is null)
            {
                throw new InvalidOperationException("Paired session is missing.");
            }

            if (_isPairedFlow)
                StatusMessage = "Scanning this device…";

            _collectionResult = await _scan.RunAsync(
                AdminModeSelected,
                progress,
                ScanSteps,
                _scanCts.Token);

            _scanCompletedAt = DateTime.UtcNow;
            ScanProgress = 100;
            StatusMessage = "Scan complete.";

            ResetReportPreparation();
            CurrentPage = AppPage.FunctionalTests;
            FunctionalTests.CurrentStep = FunctionalTestStep.Hub;
            StatusMessage = "Complete interactive checks, or skip to continue.";
        }
        catch (Exception ex)
        {
            _logger.Error($"Scan failed (api={_settings.ApiBaseUrl}, channel={_settings.BuildChannel})", ex);
            ShowError(MapScanError(ex));
            CurrentPage = AppPage.Permission;
        }
    }

    [RelayCommand]
    private void ContinueToReport()
    {
        FunctionalTests.CurrentStep = FunctionalTestStep.Complete;
        StartPrepareReportIfNeeded();
    }

    [RelayCommand(CanExecute = nameof(CanViewInspectionReport))]
    private void ViewInspectionReport()
    {
        if (!IsReportReady || _collectionResult is null)
            return;

        HasError = false;
        CurrentPage = AppPage.ResultPreview;
        StatusMessage = "Review your report before submitting.";
    }

    [RelayCommand(CanExecute = nameof(CanRetryReportPreparation))]
    private void RetryReportPreparation() => StartPrepareReportIfNeeded(force: true);

    private bool CanViewInspectionReport() => IsReportReady && !IsPreparingReport;

    private bool CanRetryReportPreparation() => !IsPreparingReport && !IsReportReady;

    partial void OnIsReportReadyChanged(bool value)
    {
        NotifyReportCommands();
        if (value && _isPairedFlow && _pairedContext is not null)
            _ = SubmitCertification();
    }

    partial void OnIsPreparingReportChanged(bool value) => NotifyReportCommands();

    private void NotifyReportCommands()
    {
        ViewInspectionReportCommand.NotifyCanExecuteChanged();
        RetryReportPreparationCommand.NotifyCanExecuteChanged();
    }

    private void OnFunctionalTestsReachedComplete() => StartPrepareReportIfNeeded();

    private void ResetReportPreparation()
    {
        IsPreparingReport = false;
        IsReportReady = false;
        ReportStatusMessage = "";
        ReportPreparationFailed = false;
        InspectionReport = "";
        InspectionReportSummary = null;
        Summary = null;
        _prepareReportTask = null;
        NotifyReportCommands();
    }

    private void StartPrepareReportIfNeeded(bool force = false)
    {
        if (_collectionResult is null)
            return;
        if (!force && (IsPreparingReport || IsReportReady))
            return;
        if (IsPreparingReport)
            return;

        _prepareReportTask = PrepareInspectionReportAsync();
    }

    private async Task PrepareInspectionReportAsync()
    {
        if (_collectionResult is null)
            return;

        IsPreparingReport = true;
        IsReportReady = false;
        ReportPreparationFailed = false;
        ReportStatusMessage = "Preparing inspection report…";
        NotifyReportCommands();

        try
        {
            _collectionResult.FunctionalTests = FunctionalTests.Results;
            FunctionalTests.FinalizeBeforeSubmit(_collectionResult);

            var deep = await Task.Run(async () => await _deepCert.RunAsync(
                _collectionResult,
                AdminModeSelected,
                FunctionalTests.Results,
                _scanCts?.Token ?? default,
                runStressBenchmarks: false).ConfigureAwait(false)).ConfigureAwait(true);

            _collectionResult.Certification = deep.Assessment;
            _collectionResult.Evidence = deep.Evidence;

            Summary = _quality.BuildSummary(_collectionResult, AdminModeSelected ? "Enhanced v2" : "Standard v2");
            InspectionReportSummary = _collectionResult.Certification?.Summary;
            InspectionReport = FormatInspectionReport(_collectionResult.Certification);
            IsReportReady = true;
            ReportStatusMessage = "Inspection report is ready.";
        }
        catch (Exception ex)
        {
            _logger.Error("Inspection report build failed", ex);
            ReportPreparationFailed = true;
            ReportStatusMessage = "Report preparation failed. Retry when ready.";
            ShowError($"Could not prepare inspection report: {ex.Message}");
        }
        finally
        {
            IsPreparingReport = false;
            NotifyReportCommands();
        }
    }

    private static string FormatInspectionReport(CertificationAssessmentV2? c)
    {
        if (c?.Summary is not { } s) return "";
        var lines = new List<string>
        {
            $"Recommended resale grade: {s.RecommendedResaleGrade}",
            "",
            "Device overview",
            s.DeviceOverview,
            "",
            "Health summary",
            s.HealthSummary,
            "",
            $"Battery: {s.BatteryCondition}",
            $"Storage: {s.StorageCondition}",
            $"Performance: {s.PerformanceRating}",
            $"Security: {s.SecurityRating}",
            "",
            "Functional tests",
        };
        lines.AddRange(s.FunctionalTestLines.Count > 0 ? s.FunctionalTestLines : [s.FunctionalTestResults]);
        if (!string.IsNullOrWhiteSpace(s.RefurbisherNotes))
        {
            lines.Add("");
            lines.Add("Refurbisher notes");
            lines.Add(s.RefurbisherNotes);
        }
        return string.Join(Environment.NewLine, lines);
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
        SubmissionStatus = _pairedContext is null
            ? "Securing your scan with a one-time session…"
            : "Uploading secure scan…";
        HasError = false;

        try
        {
            await Task.Delay(300);
            SubmissionStatus = _pairedContext is null
                ? "Creating your trusted certificate…"
                : "Creating your trusted certificate…";

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
            else if (_pairedContext is not null)
            {
                var paired = _pairedContext;
                result = await _sessionFlow!.UploadPairedAsync(
                    paired.UploadToken,
                    paired.ScanSessionId,
                    _collectionResult,
                    _scanStartedAt,
                    _scanCompletedAt,
                    AdminModeSelected,
                    _scanCts?.Token ?? default);
                _pairedContext = null;
                _isPairedFlow = false;
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
            StatusMessage = "Certification complete";
            CurrentPage = AppPage.CertificateSuccess;
        }
        catch (Exception ex)
        {
            _logger.Error($"Submit failed (api={_settings.ApiBaseUrl}, channel={_settings.BuildChannel})", ex);
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

    private string MapScanError(Exception ex) => ex switch
    {
        InvalidOperationException ioe => ioe.Message,
        HttpRequestException hre =>
            $"Unable to reach VerifyTech. Check your internet connection and try again.\n\nServer: {_settings.ApiBaseUrl}\n\nDetails: {hre.Message}",
        UnauthorizedAccessException => "Permission was denied for part of this scan. You can retry with a standard scan.",
        _ => $"Something went wrong during the scan. Please try again.\n\nServer: {_settings.ApiBaseUrl}\n\nDetails: {ex.Message}",
    };
}
