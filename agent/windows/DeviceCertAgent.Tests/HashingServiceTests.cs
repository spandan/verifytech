using DeviceCertAgent.Core.Models;
using DeviceCertAgent.Core.Services;
using Xunit;

namespace DeviceCertAgent.Tests;

public class HashingServiceTests
{
    [Fact]
    public void HashIdentifier_IsDeterministic()
    {
        var a = HashingService.HashIdentifier("SN-12345");
        var b = HashingService.HashIdentifier("sn-12345");
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void ScanQuality_ComputesCompleteness()
    {
        var result = new CollectionResult
        {
            Tier1 = new Tier1Identity
            {
                Manufacturer = "Dell",
                Model = "Latitude",
                SerialNumberHash = new string('a', 64),
                HardwareUuidHash = new string('b', 64),
                CpuModel = "i5",
                RamTotalGb = 16,
                StorageTotalGb = 512,
            },
            Tier2 = new Tier2Value
            {
                Storage = [new StorageDriveInfo { CapacityGb = 512 }],
                Battery = new BatteryInfo { Present = true, HealthPercent = 90 },
                Display = new DisplayInfo { Resolution = "1920x1080" },
                Graphics = new GraphicsInfo { GpuModel = "Intel" },
                FunctionalReadiness = new FunctionalReadiness
                {
                    CameraPresent = true,
                    WifiPresent = true,
                    KeyboardPresent = true,
                    BluetoothPresent = true,
                },
            },
        };

        var service = new ScanQualityService();
        var pct = service.ComputeCompleteness(result);
        Assert.True(pct >= 80);
        Assert.True(service.MeetsTier1Minimum(result));
    }
}
