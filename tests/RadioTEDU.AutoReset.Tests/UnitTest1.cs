using RadioTEDU.AutoReset.Models;
using RadioTEDU.AutoReset.Services;
using Xunit;

namespace RadioTEDU.AutoReset.Tests;

public class ScheduleEngineTests
{
    [Fact]
    public void CalculateNextReset_24Hours_BeforeTargetHour_ReturnsToday()
    {
        var config = new ResetConfig
        {
            IntervalHours = 24,
            TargetHour = 3,
            TargetMinute = 0
        };

        var now = new DateTime(2026, 9, 14, 1, 30, 0); // 01:30 AM
        var next = ScheduleEngine.CalculateNextReset(config, now);

        Assert.Equal(new DateTime(2026, 9, 14, 3, 0, 0), next);
    }

    [Fact]
    public void CalculateNextReset_24Hours_AfterTargetHour_ReturnsTomorrow()
    {
        var config = new ResetConfig
        {
            IntervalHours = 24,
            TargetHour = 3,
            TargetMinute = 0
        };

        var now = new DateTime(2026, 9, 14, 14, 0, 0); // 14:00 PM
        var next = ScheduleEngine.CalculateNextReset(config, now);

        Assert.Equal(new DateTime(2026, 9, 15, 3, 0, 0), next);
    }

    [Fact]
    public void CalculateNextReset_48Hours_StepsBy48Hours()
    {
        var lastReset = new DateTime(2026, 9, 14, 3, 0, 0);
        var config = new ResetConfig
        {
            IntervalHours = 48,
            TargetHour = 3,
            TargetMinute = 0,
            LastResetTime = lastReset
        };

        var now = new DateTime(2026, 9, 14, 10, 0, 0);
        var next = ScheduleEngine.CalculateNextReset(config, now);

        Assert.Equal(new DateTime(2026, 9, 16, 3, 0, 0), next);
    }

    [Fact]
    public void GeneratePreviewSchedule_ReturnsConsecutiveDates()
    {
        var config = new ResetConfig
        {
            IntervalHours = 24,
            TargetHour = 3,
            TargetMinute = 0
        };

        var now = new DateTime(2026, 9, 14, 12, 0, 0);
        var preview = ScheduleEngine.GeneratePreviewSchedule(config, now, days: 7);

        Assert.NotEmpty(preview);
        Assert.True(preview.Count >= 7);
        Assert.Equal(new DateTime(2026, 9, 15, 3, 0, 0), preview[0]);
        Assert.Equal(new DateTime(2026, 9, 16, 3, 0, 0), preview[1]);
        Assert.Equal(new DateTime(2026, 9, 17, 3, 0, 0), preview[2]);
    }

    [Fact]
    public void FormatRemainingTime_FormatsCorrectly()
    {
        var span = new TimeSpan(14, 25, 10);
        var text = ScheduleEngine.FormatRemainingTime(span);

        Assert.Contains("14 saat", text);
        Assert.Contains("25 dakika", text);
        Assert.Contains("10 saniye", text);
    }

    [Fact]
    public void ResetConfig_DefaultWatchdogSettings_AreNativelyOff()
    {
        var config = new ResetConfig();

        // Must be natively OFF by default as requested
        Assert.False(config.CpuWatchdogEnabled);
        Assert.False(config.RamWatchdogEnabled);

        // Auto restart on unexpected exit must be ON
        Assert.True(config.AutoRestartOnUnexpectedExit);

        // Default thresholds
        Assert.Equal(95.0, config.CpuThresholdPercent);
        Assert.Equal(15, config.CpuSustainedMinutes);
        Assert.Equal(1024.0, config.RamThresholdMb);
        Assert.Equal(15, config.RamSustainedMinutes);
    }

    [Fact]
    public void ConfigService_FactoryReset_DeletesConfigurationAndHistory()
    {
        var service = new ConfigService();
        var testConfig = new ResetConfig { TargetExePath = @"C:\dummy.exe" };
        service.SaveConfig(testConfig);
        service.AppendHistory(new ResetLogEntry { Message = "Test" });

        Assert.True(service.HasConfig());

        var resetResult = service.FactoryReset();
        Assert.True(resetResult);
        Assert.False(service.HasConfig());
    }

    [Fact]
    public void ResetConfig_MultiApp_DefaultsToEmptyList()
    {
        var config = new ResetConfig();

        // TargetApps should start empty with no suggested/hardcoded apps
        Assert.NotNull(config.TargetApps);
        Assert.Empty(config.TargetApps);
        Assert.Equal(string.Empty, config.TargetExePath);
    }

    [Fact]
    public void ResetConfig_MultiApp_SupportsMultipleAppsAndBackwardCompatibility()
    {
        var config = new ResetConfig();
        config.TargetApps.Add(new WatchedApp { ExePath = @"C:\App1.exe", Enabled = true });
        config.TargetApps.Add(new WatchedApp { ExePath = @"C:\App2.exe", Enabled = true });

        Assert.Equal(2, config.TargetApps.Count);
        Assert.Equal(@"C:\App1.exe", config.TargetExePath);
        Assert.Equal("App1.exe", config.TargetApps[0].Name);

        // Setting TargetExePath directly updates or inserts
        config.TargetExePath = @"C:\App3.exe";
        Assert.Equal(@"C:\App3.exe", config.TargetApps[0].ExePath);
    }
}
