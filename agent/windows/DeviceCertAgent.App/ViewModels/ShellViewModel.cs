using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCertAgent.App;
using DeviceCertAgent.App.Services;
using DeviceCertAgent.Core.Configuration;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Services;
using DeviceCertAgent.Core.Services.V2;

namespace DeviceCertAgent.App.ViewModels;

public enum AppPage
{
    Welcome,
    UnpairedWaiting,
    EligibilityChecking,
    NotEligible,
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
    private readonly EligibilityProbeService _eligibilityProbe = new();
    private readonly EligibilityEngine _eligibilityEngine = new();
    private readonly LocalCacheCleanupService _cache = new();
    private readonly AgentLogger _logger = new();
    private readonly ScanSessionFlowService? _sessionFlow;
    private readonly MockScanSessionFlowService? _mockFlow;
    private readonly AppLaunchOptions _launch;
    private CancellationTokenSource? _scanCts;
    private bool _openReportWhenReady;
    private bool _pairingMandatory;
    private bool _linkedToAccount;
    private bool _pairingForSubmit;

    public bool IsAccountLinked => _pairedContext is not null;
    public bool ShowUnlinkedAccountBanner =>
        CurrentPage == AppPage.ResultPreview && _pairedContext is null;
    public bool ShowAccountLinkedBanner =>
        CurrentPage == AppPage.ResultPreview && _pairedContext is not null;
    public bool ShowSaveToAccountBeforeSubmit =>
        CurrentPage == AppPage.ResultPreview && _pairedContext is null && IsReportReady;
    public bool ShowSaveToAccountAfterSubmit =>
        CurrentPage == AppPage.CertificateSuccess && !_linkedToAccount && CertResult is not null;
    public bool PairingForSubmit => _pairingForSubmit;
    public bool ShowCertificateLinkedToAccount => _linkedToAccount;

    public ShellViewModel(AgentRuntimeSettings settings, AppLaunchOptions launch)
    {
        _settings = settings;
        _launch = launch;
        if (settings.MockApi)
            _mockFlow = new MockScanSessionFlowService();
        else
            _sessionFlow = new ScanSessionFlowService(settings);

        StatusMessage = "Ready when you are.";
        BrandSubtitle = "Device certification";
        FunctionalTests.ReachedCompleteStep += OnFunctionalTestsReachedComplete;
        FunctionalTests.LeftCompleteStep += OnFunctionalTestsLeftComplete;
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
    [ObservableProperty] private string _agentPairingCode = "";
    [ObservableProperty] private string _pairingCodeExpiredMessage = "";
    [ObservableProperty] private string _linkedAccountName = "";
    [ObservableProperty] private string _eligibilityMessage = "";
    [ObservableProperty] private bool _isPreparingReport;
    [ObservableProperty] private bool _isReportReady;
    [ObservableProperty] private string _reportStatusMessage = "";
    [ObservableProperty] private bool _reportPreparationFailed;

    public ObservableCollection<ScanStepProgress> ScanSteps { get; } = [];

    private CollectionResult? _collectionResult;
    private ScanSessionStartResponse? _activeSession;
    private PairedScanContext? _pairedContext;
    private bool _isPairedFlow;
    private string? _certificationToken;
    private string? _certificationSessionId;
    private string? _certificationUserId;
    private string _deviceNonce = "";
    private CancellationTokenSource? _pairingPollCts;
    private string? _expectedDeviceType;
    private DateTime _scanStartedAt;
    private DateTime _scanCompletedAt;
    private Task? _prepareReportTask;

    [RelayCommand]
    private void BeginCertifyDevice()
    {
        _linkedToAccount = false;
        _pairedContext = null;
        _isPairedFlow = false;
        _pairingForSubmit = false;
        HasError = false;
        ErrorMessage = "";
        _ = BeginCertifyDeviceAsync();
    }

    [RelayCommand]
    private void SaveToAccountBeforeSubmit()
    {
        if (CurrentPage != AppPage.ResultPreview || !IsReportReady)
            return;

        HasError = false;
        ErrorMessage = "";
        _ = StartAccountPairingAsync(forSubmit: true);
    }

    public void InitializeLaunchFlow()
    {
        _deviceNonce = _launch.DeviceNonce;

        if (_launch.RequestAccountCertificationFlow)
        {
            _ = BeginCertifyDeviceAsync();
            return;
        }

        CurrentPage = AppPage.Welcome;
        StatusMessage = "Ready when you are.";
    }

    public void HandleDeepLinkLaunch()
    {
        if (CurrentPage == AppPage.Welcome)
            _ = BeginCertifyDeviceAsync();
    }

    private async Task BeginCertifyDeviceAsync()
    {
        if (_settings.MockApi)
        {
            CurrentPage = AppPage.Disclosure;
            StatusMessage = "Review what we collect, then run the scan.";
            return;
        }

        CurrentPage = AppPage.EligibilityChecking;
        StatusMessage = "Eligibility check…";
        if (!await RunEligibilityGateAsync("laptop"))
            return;

        CurrentPage = AppPage.Disclosure;
        StatusMessage = "Review what we collect, then run the scan.";
    }

    partial void OnCurrentPageChanged(AppPage value)
    {
        OnPropertyChanged(nameof(ShowUnlinkedAccountBanner));
        OnPropertyChanged(nameof(ShowAccountLinkedBanner));
        OnPropertyChanged(nameof(ShowSaveToAccountBeforeSubmit));
        OnPropertyChanged(nameof(ShowSaveToAccountAfterSubmit));
        OnPropertyChanged(nameof(PairingForSubmit));
    }

    partial void OnIsReportReadyChanged(bool value)
    {
        NotifyReportCommands();
        OnPropertyChanged(nameof(ShowSaveToAccountBeforeSubmit));
        if (value && _openReportWhenReady)
        {
            _openReportWhenReady = false;
            ViewInspectionReport();
        }
    }

    private void NotifyPairingUi()
    {
        OnPropertyChanged(nameof(PairingForSubmit));
    }

    private async Task StartAccountPairingAsync(bool forSubmit)
    {
        if (CurrentPage == AppPage.UnpairedWaiting && !string.IsNullOrWhiteSpace(AgentPairingCode))
            return;

        StopPairingPoll();
        _pairingForSubmit = forSubmit;
        _pairingMandatory = false;
        _isPairedFlow = false;
        HasError = false;
        ErrorMessage = "";
        NotifyPairingUi();

        if (_settings.MockApi)
        {
            ShowError("Account pairing is not available in mock mode.");
            if (forSubmit)
                CurrentPage = AppPage.ResultPreview;
            return;
        }

        CurrentPage = AppPage.UnpairedWaiting;
        StatusMessage = forSubmit
            ? "Link your Certronx account — then return here to submit."
            : "Pair with your Certronx account.";

        try
        {
            await RefreshAgentPairingCodeAsync(showExpiredNotice: false);
            StartPairingPoll();
        }
        catch (Exception ex)
        {
            _logger.Error($"Account pairing failed (api={_settings.ApiBaseUrl})", ex);
            ShowError(MapScanError(ex));
            if (forSubmit && _collectionResult is not null)
                CurrentPage = AppPage.ResultPreview;
        }
    }

    private void StartPairingPoll()
    {
        StopPairingPoll();
        _pairingPollCts = new CancellationTokenSource();
        _ = PollAgentPairingAsync(_pairingPollCts.Token);
    }

    private void StopPairingPoll()
    {
        _pairingPollCts?.Cancel();
        _pairingPollCts?.Dispose();
        _pairingPollCts = null;
    }

    private async Task PollAgentPairingAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var status = await _sessionFlow!.GetAgentPairingStatusForDeviceAsync(_deviceNonce, ct);

                if (string.Equals(status.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                {
                    await RefreshAgentPairingCodeAsync(showExpiredNotice: true);
                    await Task.Delay(3000, ct);
                    continue;
                }

                if (string.Equals(status.Status, "PAIRED", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentPage = AppPage.PairingConnecting;
                    StatusMessage = string.IsNullOrWhiteSpace(status.CertificationToken)
                        ? "Connected on Certronx — finishing account link…"
                        : "Account connected — finishing setup…";
                    if (string.IsNullOrWhiteSpace(status.CertificationToken))
                    {
                        await Task.Delay(2000, ct);
                        continue;
                    }

                    try
                    {
                        await CompleteAgentPairingAsync(status);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Pairing completed on server but agent setup failed; will retry", ex);
                        StatusMessage = "Account connected — finishing setup…";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error($"Pairing poll failed (api={_settings.ApiBaseUrl})", ex);
            }

            await Task.Delay(3000, ct);
        }
    }

    private async Task RefreshAgentPairingCodeAsync(bool showExpiredNotice)
    {
        PairingCodeExpiredMessage = showExpiredNotice
            ? "Pairing code expired. A new pairing code has been generated."
            : "";

        var created = await _sessionFlow!.CreateAgentPairingAsync(_deviceNonce);
        AgentPairingCode = created.PairingCode;
        StatusMessage = "Click Pair with your account — your browser will open Certronx to connect this PC.";
    }

    private async Task CompleteAgentPairingAsync(AgentPairingStatusResponse status)
    {
        if (string.IsNullOrWhiteSpace(status.CertificationToken))
            throw new InvalidOperationException("Pairing completed without a certification token.");

        _certificationSessionId = status.SessionId;
        _certificationUserId = status.UserId;
        _certificationToken = status.CertificationToken;
        _linkedToAccount = true;
        LinkedAccountName = "your Certronx account";
        StopPairingPoll();
        _pairingForSubmit = false;
        NotifyPairingUi();

        var bootstrap = await Task.Run(() => HardwareFingerprintService.CollectPairingBootstrap(out _));
        var fingerprint = HardwareFingerprintService.Compute(bootstrap);
        var begin = await _sessionFlow!.BeginCertificationScanAsync(status.CertificationToken, fingerprint);
        _pairedContext = new PairedScanContext
        {
            UploadToken = begin.UploadToken,
            ScanSessionId = begin.ScanSessionId,
            LinkedAccountName = begin.LinkedAccountName ?? LinkedAccountName,
        };
        LinkedAccountName = begin.LinkedAccountName ?? LinkedAccountName;

        if (_collectionResult is not null && IsReportReady)
        {
            CurrentPage = AppPage.ResultPreview;
            StatusMessage = "Account linked — submit to save this certificate to your dashboard.";
            OnPropertyChanged(nameof(ShowUnlinkedAccountBanner));
            OnPropertyChanged(nameof(ShowAccountLinkedBanner));
            OnPropertyChanged(nameof(ShowSaveToAccountBeforeSubmit));
            return;
        }

        throw new InvalidOperationException("Pairing completed but the inspection report is not ready.");
    }

    [RelayCommand]
    private void OpenCertronxPairingPage()
    {
        if (string.IsNullOrWhiteSpace(AgentPairingCode))
        {
            ShowError("Pairing code is not ready yet. Wait a moment and try again.");
            return;
        }

        var url =
            $"{AgentConfig.PublicBaseUrlFor(_settings.AppEnv)}/pair?code={Uri.EscapeDataString(AgentPairingCode.Trim())}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private async Task RefreshPairingCode()
    {
        if (_settings.MockApi || _sessionFlow is null)
            return;

        try
        {
            HasError = false;
            await RefreshAgentPairingCodeAsync(showExpiredNotice: false);
            StartPairingPoll();
        }
        catch (Exception ex)
        {
            _logger.Error($"Refresh pairing code failed (api={_settings.ApiBaseUrl})", ex);
            ShowError(MapScanError(ex));
        }
    }

    [RelayCommand]
    private void BackFromPairingToReport()
    {
        StopPairingPoll();
        _pairingForSubmit = false;
        NotifyPairingUi();
        if (_collectionResult is not null && IsReportReady)
        {
            CurrentPage = AppPage.ResultPreview;
            StatusMessage = "Review your report before submitting.";
            return;
        }

        CurrentPage = AppPage.Welcome;
    }

    [RelayCommand]
    private void OpenSaveToAccountPage()
    {
        if (string.IsNullOrWhiteSpace(CertResult?.CertificateCode))
            return;

        var url =
            $"{AgentConfig.PublicBaseUrlFor(_settings.AppEnv)}/save?code={Uri.EscapeDataString(CertResult.CertificateCode.Trim())}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true,
        });
    }

    private async Task<bool> RunEligibilityGateAsync(string? expectedDeviceType)
    {
        CurrentPage = AppPage.EligibilityChecking;
        StatusMessage = "Checking device eligibility…";

        var probe = await Task.Run(() => _eligibilityProbe.Collect(expectedDeviceType));
        var result = _eligibilityEngine.Evaluate(probe);
        if (result.Eligible)
        {
            StatusMessage = "Device eligible for certification.";
            return true;
        }

        EligibilityMessage = result.UserMessage ?? result.Reason ?? "This device is not eligible for certification.";
        CurrentPage = AppPage.NotEligible;
        StatusMessage = "Device not eligible";
        return false;
    }

    [RelayCommand]
    private void CloseNotEligible() => Application.Current.Shutdown();

    [RelayCommand]
    private void Cancel()
    {
        StopPairingPoll();
        Application.Current.Shutdown();
    }

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
            "Certronx will restart with administrator privileges.",
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
                AppBranding.AppName,
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
                AppBranding.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            CurrentPage = AppPage.Permission;
            return;
        }

        AdminModeSelected = true;
        CurrentPage = AppPage.Permission;
        _ = RunScanAsync();
    }

    private async Task RunScanAsync(bool skipSessionStart = false, bool skipEligibility = false)
    {
        HasError = false;
        ErrorMessage = "";

        if (!skipEligibility && !await RunEligibilityGateAsync(_expectedDeviceType ?? "laptop"))
            return;

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
                ?? "Scanning this device…";
        });

        try
        {
            _scanStartedAt = DateTime.UtcNow;
            if (!skipSessionStart)
            {
                StatusMessage = "Connecting securely to Certronx…";

                if (_settings.MockApi)
                    _activeSession = await _mockFlow!.StartSessionAsync(_scanCts.Token);
                else
                    _activeSession = await _sessionFlow!.StartSessionAsync(_scanCts.Token);
            }
            else if (_activeSession is null)
            {
                throw new InvalidOperationException("Scan session is missing.");
            }

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
            FunctionalTests.ResetForNewRun();
            StatusMessage = "Run the interactive checks, or skip optional checks and start with display.";
        }
        catch (Exception ex)
        {
            _logger.Error($"Scan failed (api={_settings.ApiBaseUrl}, channel={_settings.BuildChannel})", ex);
            ShowError(MapScanError(ex));
            CurrentPage = AppPage.Permission;
        }
    }

    private void OnFunctionalTestsReachedComplete() => StartPrepareReportIfNeeded();

    private void OnFunctionalTestsLeftComplete() => ResetReportPreparation();

    [RelayCommand(CanExecute = nameof(CanViewInspectionReport))]
    private void ViewInspectionReport()
    {
        if (!IsReportReady || _collectionResult is null)
            return;

        HasError = false;
        OnPropertyChanged(nameof(ShowCertificateLinkedToAccount));
        OnPropertyChanged(nameof(ShowUnlinkedAccountBanner));
        OnPropertyChanged(nameof(ShowAccountLinkedBanner));
        CurrentPage = AppPage.ResultPreview;
        StatusMessage = "Review your report before submitting.";
    }

    [RelayCommand(CanExecute = nameof(CanRetryReportPreparation))]
    private void RetryReportPreparation() => StartPrepareReportIfNeeded(force: true);

    private bool CanViewInspectionReport() => IsReportReady && !IsPreparingReport;

    private bool CanRetryReportPreparation() => !IsPreparingReport && !IsReportReady;

    partial void OnIsPreparingReportChanged(bool value) => NotifyReportCommands();

    private void NotifyReportCommands()
    {
        ViewInspectionReportCommand.NotifyCanExecuteChanged();
        RetryReportPreparationCommand.NotifyCanExecuteChanged();
    }

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
            ReportStatusMessage = "";
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
        if (_collectionResult is null || _activeSession is null)
        {
            ShowError("Scan session is not ready. Run the scan again from the start.");
            return;
        }

        if (!_quality.MeetsTier1Minimum(_collectionResult))
        {
            ShowError("We could not collect enough device identity to issue a certificate. Try again on this device.");
            return;
        }

        CurrentPage = AppPage.Submitting;
        HasError = false;

        try
        {
            ScanSessionSubmitResponse result;
            var savedToAccount = _pairedContext is not null;
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
            if (savedToAccount)
                _linkedToAccount = true;
            StatusMessage = "";
            CurrentPage = AppPage.CertificateSuccess;
            OnPropertyChanged(nameof(ShowCertificateLinkedToAccount));
            OnPropertyChanged(nameof(ShowSaveToAccountAfterSubmit));
        }
        catch (Exception ex)
        {
            _logger.Error($"Submit failed (api={_settings.ApiBaseUrl}, channel={_settings.BuildChannel})", ex);
            ShowError(MapScanError(ex));
            CurrentPage = AppPage.ResultPreview;
        }
    }

    [RelayCommand]
    private void CopyAgentPairingCode()
    {
        if (string.IsNullOrWhiteSpace(AgentPairingCode))
            return;
        Clipboard.SetText(AgentPairingCode);
        StatusMessage = "Pairing code copied.";
    }

    [RelayCommand]
    private void CopyCertificateCode()
    {
        if (_certResult?.CertificateCode is not null)
        {
            Clipboard.SetText(_certResult.CertificateCode);
            StatusMessage = "Certificate code copied.";
        }
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
        MessageBox.Show("Local scan cache removed.", AppBranding.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void KeepInstalled() => Application.Current.Shutdown();

    [RelayCommand]
    private void ExitApp() => Application.Current.Shutdown();

    [RelayCommand]
    private void BackToWelcome()
    {
        HasError = false;
        _linkedToAccount = false;
        _isPairedFlow = false;
        _pairedContext = null;
        _pairingForSubmit = false;
        StopPairingPoll();
        CurrentPage = AppPage.Welcome;
    }

    private void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        MessageBox.Show(message, AppBranding.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private string MapScanError(Exception ex) => ex switch
    {
        InvalidOperationException ioe => ioe.Message,
        HttpRequestException hre when hre.Message.Contains("500")
            => "Certronx could not create your certificate (server error). Please try again in a few minutes.",
        HttpRequestException hre when hre.Message.Contains("422")
            => ExtractApiDetail(hre.Message, "Submission was rejected. Please scan again."),
        HttpRequestException
            => "Could not reach Certronx. Check your internet connection and try again.",
        UnauthorizedAccessException => "Permission was denied for part of this scan. You can retry with a standard scan.",
        _ => "Something went wrong during the scan. Please try again.",
    };

    private static string ExtractApiDetail(string message, string fallback)
    {
        const string marker = "): ";
        var idx = message.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return fallback;
        var detail = message[(idx + marker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(detail) || detail.StartsWith('{') ? fallback : detail;
    }
}
