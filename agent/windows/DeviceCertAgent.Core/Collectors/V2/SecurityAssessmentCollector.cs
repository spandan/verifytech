using DeviceCertAgent.Core.Models.V2;
using DeviceCertAgent.Core.Utilities;
using Microsoft.Win32;

namespace DeviceCertAgent.Core.Collectors.V2;

public sealed class SecurityAssessmentCollector
{
    public SecurityAssessment Collect(bool adminMode, List<string> warnings)
    {
        var s = new SecurityAssessment();

        var tpmExists = WmiHelper.DeviceExists("SELECT * FROM Win32_Tpm");
        if (tpmExists)
        {
            s.TpmPresent = TriStateValue.Verified(true, "Win32_Tpm", "wmi");
            var ver = WmiHelper.FirstString("SELECT SpecVersion FROM Win32_Tpm", "SpecVersion");
            if (!string.IsNullOrWhiteSpace(ver))
                s.TpmVersion = ConfidenceValue<string?>.Collected(ver, "Win32_Tpm", "wmi");
            var enabled = WmiHelper.FirstString("SELECT IsEnabled_InitialValue FROM Win32_Tpm", "IsEnabled_InitialValue");
            if (bool.TryParse(enabled, out var en))
                s.TpmEnabled = TriStateValue.Verified(en, "Win32_Tpm", "wmi");
            else
                s.TpmEnabled = TriStateValue.Unknown("wmi");
        }
        else
        {
            s.TpmPresent = TriStateValue.Unknown("wmi");
            s.TpmEnabled = TriStateValue.Unknown("wmi");
        }

        s.SecureBoot = ReadSecureBoot();
        var bitlocker = PowerShellHelper.Run(
            "Try { (Get-BitLockerVolume -ErrorAction Stop | Select-Object -First 1 | ForEach-Object { $_.VolumeStatus.ToString() + '|' + $_.ProtectionStatus.ToString() }) } Catch { 'unknown' }")
            ?? "unknown";
        s.BitLockerStatus = ConfidenceValue<string?>.Collected(
            bitlocker.Replace("\r", "").Replace("\n", ""), "Get-BitLockerVolume", "powershell", ConfidenceLevel.Medium);

        var encrypted = bitlocker.Contains("On", StringComparison.OrdinalIgnoreCase);
        s.DeviceEncryption = bitlocker == "unknown"
            ? TriStateValue.Unknown("bitlocker")
            : TriStateValue.Verified(encrypted, "Get-BitLockerVolume", "powershell");

        s.VbsEnabled = ReadRegistryBool(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity");
        s.CredentialGuard = ReadRegistryBool(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableCredentialGuard");

        var score = 50;
        if (s.TpmPresent.Value == true) score += 15;
        if (s.SecureBoot.Value == true) score += 15;
        if (s.DeviceEncryption.Value == true) score += 20;
        s.SecurityScore = ConfidenceValue<int?>.Collected(Math.Min(100, score), "engine", "security_score", ConfidenceLevel.Medium);

        return s;
    }

    private static TriStateValue ReadSecureBoot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var value = key?.GetValue("UEFISecureBootEnabled");
            if (value is null) return TriStateValue.Unknown("registry");
            return TriStateValue.Verified(Convert.ToInt32(value) == 1, "registry", "UEFISecureBootEnabled");
        }
        catch
        {
            return TriStateValue.Unknown("registry");
        }
    }

    private static TriStateValue ReadRegistryBool(string path, string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            var v = key?.GetValue(name);
            if (v is null) return TriStateValue.Unknown("registry");
            return TriStateValue.Verified(Convert.ToInt32(v) == 1, "registry", name);
        }
        catch
        {
            return TriStateValue.Unknown("registry");
        }
    }
}
