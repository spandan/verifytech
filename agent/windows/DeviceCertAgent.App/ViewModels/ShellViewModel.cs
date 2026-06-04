using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Services;

namespace DeviceCertAgent.App.ViewModels;

public enum AppPage
{
    Welcome,
    Privacy,
    StandardScan,
    EnhancedOffer,
    EnhancedScan,
    Summary,
    Submission,
    CertSuccess,
    VerifyResult,
    Final,
}

public partial class ShellViewModel : ObservableObject
{
    private readonly EndpointSettings _settings;
    private readonly AppLaunchOptions _launch;
    private readonly StandardScanService _standardScan = new();
    private readonly EnhancedScanService _enhancedScan = new();
    private readonly ScanQualityService _quality = new();
    private readonly ReportAssemblyService _reportAssembly = new();
    private readonly LocalCacheCleanupService _cache = new();
    private readonly ISubmissionService _submission;
    private CancellationTokenSource? _scanCts;

    public ShellViewModel(EndpointSettings settings, AppLaunchOptions launch)
    {
        _settings = settings;
        _launch = launch;
        _submission = SubmissionServiceFactory.Create(settings);
        StatusMessage = FriendlyMessages[0];
        ApiEndpointDisplay = settings.ApiBaseUrl + (settings.MockApi ? " (mock)" : "");
    }

    [ObservableProperty] private AppPage _currentPage = AppPage.Welcome;
    [ObservableProperty] private string _selectedMode = "certify";
    [ObservableProperty] private string _certificateCode = "";
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _submissionStatus = "";
    [ObservableProperty] private bool _showTechnicalDetails;
    [ObservableProperty] private string _technicalDetails = "";
    [ObservableProperty] private ScanSummary? _summary;
    [ObservableProperty] private CertifyApiResponse? _certResult;
    [ObservableProperty] private VerifyApiResponse? _verifyResult;
    [ObservableProperty] private string _resultBadgeColor = "#16A34A";
    [ObservableProperty] private string _resultTitle = "";
    [ObservableProperty] private string _resultMessage = "";
    [ObservableProperty] private string _apiEndpointDisplay;

    public ObservableCollection<ScanStepProgress> ScanSteps { get; } = [];
    public ObservableCollection<string> ChangedComponents { get; } = [];

    public string EnvironmentLabel => $"{_settings.Environment} · {_settings.ApiBaseUrl}";

    private CollectionResult? _collectionResult;
    private DeviceReport? _report;
    private bool _enhancedIncluded;

    private static readonly string[] FriendlyMessages =
    [
        "Checking hardware identity…",
        "Reading device health signals…",
        "Preparing a buyer-friendly certificate…",
        "No personal files are being scanned.",
        "Sensitive identifiers are hashed before submission.",
    ];

    [RelayCommand]
    private void StartCertify()
    {
        SelectedMode = "certify";
        _launch.Mode = "certify";
        CurrentPage = AppPage.Privacy;
    }

    [RelayCommand]
    private void StartVerify()
    {
        if (string.IsNullOrWhiteSpace(CertificateCode))
        {
            MessageBox.Show("Please enter a certificate code.", "Device Certification Agent",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectedMode = "verify";
        _launch.Mode = "verify";
        _launch.CertificateCode = CertificateCode.ToUpperInvariant();
        CurrentPage = AppPage.Privacy;
    }

    [RelayCommand]
    private void Cancel() => Application.Current.Shutdown();

    [RelayCommand]
    private async Task ContinueToScan()
    {
        CurrentPage = AppPage.StandardScan;
        ScanSteps.Clear();
        foreach (var step in _standardScan.CreateSteps())
            ScanSteps.Add(step);

        _scanCts = new CancellationTokenSource();
        var progress = new Progress<ScanStepProgress>(p =>
        {
            ScanProgress = ScanSteps.Count(s => s.Status is ScanStepStatus.Completed or ScanStepStatus.Warning) * 100.0 / ScanSteps.Count;
            StatusMessage = FriendlyMessages[Random.Shared.Next(FriendlyMessages.Length)];
        });

        try
        {
            _collectionResult = await _standardScan.RunAsync(progress, ScanSteps, _scanCts.Token);
            ScanProgress = 100;
            StatusMessage = "Standard scan complete.";

            if (SelectedMode == "certify")
                CurrentPage = AppPage.EnhancedOffer;
            else
                await GoToSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Scan encountered an issue: {ex.Message}", "Scan", MessageBoxButton.OK, MessageBoxImage.Warning);
            CurrentPage = AppPage.Welcome;
        }
    }

    [RelayCommand]
    private async Task SkipEnhanced()
    {
        _enhancedIncluded = false;
        await GoToSummary();
    }

    [RelayCommand]
    private async Task RunEnhanced()
    {
        CurrentPage = AppPage.EnhancedScan;
        StatusMessage = "Enhanced scan may request administrator permission…";

        var admin = EnhancedScanService.IsRunningAsAdmin();
        if (!admin)
        {
            var result = MessageBox.Show(
                "Enhanced scan works best with administrator access for SMART health and advanced diagnostics.\n\nAllow attempting enhanced collection?",
                "Administrator Access",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            admin = result == MessageBoxResult.Yes && EnhancedScanService.IsRunningAsAdmin();
        }

        if (_collectionResult is not null)
        {
            await _enhancedScan.ApplyEnhancedAsync(_collectionResult, admin, msg => StatusMessage = msg, _scanCts?.Token ?? default);
            _enhancedIncluded = admin;
        }

        await GoToSummary();
    }

    private Task GoToSummary()
    {
        if (_collectionResult is null) return Task.CompletedTask;

        Summary = _quality.BuildSummary(_collectionResult, _enhancedIncluded ? "Enhanced" : "Standard");
        _report = _reportAssembly.Assemble(_collectionResult, _launch, _enhancedIncluded);
        TechnicalDetails = JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true });
        CurrentPage = AppPage.Summary;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SubmitSecurely()
    {
        if (_report is null || _collectionResult is null) return;

        if (!_quality.MeetsTier1Minimum(_collectionResult))
        {
            MessageBox.Show("Not enough device identity data was collected to submit a certificate.", "Submission",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CurrentPage = AppPage.Submission;
        SubmissionStatus = "Encrypting and hashing identifiers…";

        try
        {
            await Task.Delay(400);
            SubmissionStatus = "Sending report to certification server…";

            if (SelectedMode == "verify")
            {
                var vr = await _submission.VerifyAsync(_report);
                _verifyResult = vr;
                ApplyVerifyResult(vr);
                CurrentPage = AppPage.VerifyResult;
            }
            else
            {
                var cr = await _submission.CertifyAsync(_report);
                _certResult = cr;
                _cache.SaveTemporaryReport(_report);
                CurrentPage = AppPage.CertSuccess;
            }
        }
        catch (HttpRequestException ex)
        {
            SubmissionStatus = "Connection failed. Check your internet and API URL.";
            MessageBox.Show(ex.Message, "Submission Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CurrentPage = AppPage.Summary;
        }
        catch (Exception ex)
        {
            SubmissionStatus = "Something went wrong.";
            MessageBox.Show(ex.Message, "Submission Error", MessageBoxButton.OK, MessageBoxImage.Error);
            CurrentPage = AppPage.Summary;
        }
    }

    private void ApplyVerifyResult(VerifyApiResponse vr)
    {
        ChangedComponents.Clear();
        foreach (var c in vr.Changes)
            ChangedComponents.Add($"{c.Field}: changed");

        switch (vr.Result)
        {
            case "CERTIFIED_MATCH":
                ResultBadgeColor = "#16A34A";
                ResultTitle = "Certified Match";
                ResultMessage = "This device matches the certified report.";
                break;
            case "CERTIFIED_WITH_CHANGES":
                ResultBadgeColor = "#D97706";
                ResultTitle = "Certified With Changes";
                ResultMessage = "Same device, but some value-related components changed.";
                break;
            case "DEVICE_MISMATCH":
                ResultBadgeColor = "#DC2626";
                ResultTitle = "Device Mismatch";
                ResultMessage = "This certificate exists, but this device does not match.";
                break;
            case "CERTIFICATE_EXPIRED":
                ResultBadgeColor = "#D97706";
                ResultTitle = "Certificate Expired";
                ResultMessage = "This certificate exists, but is no longer active.";
                break;
            case "CERTIFICATE_NOT_FOUND":
                ResultBadgeColor = "#DC2626";
                ResultTitle = "Certificate Not Found";
                ResultMessage = "This certificate was not issued by our platform.";
                break;
            default:
                ResultBadgeColor = "#5B677A";
                ResultTitle = vr.Result;
                ResultMessage = vr.Message;
                break;
        }
    }

    [RelayCommand]
    private void CopyCertificateCode()
    {
        if (_certResult?.CertificateCode is not null)
            Clipboard.SetText(_certResult.CertificateCode);
    }

    [RelayCommand]
    private void CopyCertificateLink()
    {
        if (_certResult?.CertificateUrl is not null)
            Clipboard.SetText(_certResult.CertificateUrl);
    }

    [RelayCommand]
    private void OpenCertificate()
    {
        if (_certResult?.CertificateUrl is not null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_certResult.CertificateUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ClearLocalCache()
    {
        _cache.ClearCache();
        MessageBox.Show("Local scan files removed.", "Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Finish() => CurrentPage = AppPage.Final;

    [RelayCommand]
    private void ExitApp() => Application.Current.Shutdown();

    [RelayCommand]
    private void BackToWelcome() => CurrentPage = AppPage.Welcome;

    [RelayCommand]
    private void ToggleTechnicalDetails() => ShowTechnicalDetails = !ShowTechnicalDetails;
}
