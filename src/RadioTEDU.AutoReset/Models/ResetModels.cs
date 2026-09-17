namespace RadioTEDU.AutoReset.Models;

public class WatchedApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ExePath { get; set; } = string.Empty;
    public string Name => Path.GetFileName(ExePath);
    public bool Enabled { get; set; } = true;
    public DateTime AddedAt { get; set; } = DateTime.Now;

    public override string ToString() => $"{Name} ({ExePath})";
}

public class ResetConfig
{
    // Multi-Application List (Empty by default, NO hardcoded defaults)
    public List<WatchedApp> TargetApps { get; set; } = new();

    // Legacy fallback compatibility property
    public string TargetExePath
    {
        get => TargetApps.FirstOrDefault()?.ExePath ?? string.Empty;
        set
        {
            if (!string.IsNullOrEmpty(value) && !TargetApps.Any(a => a.ExePath.Equals(value, StringComparison.OrdinalIgnoreCase)))
            {
                TargetApps.Insert(0, new WatchedApp { ExePath = value });
            }
        }
    }

    public int IntervalHours { get; set; } = 24; // 24 or 48
    public int TargetHour { get; set; } = 3;     // 0-23 (e.g. 3 for 03:00)
    public int TargetMinute { get; set; } = 0;   // 0-59
    public int GracefulWaitSeconds { get; set; } = 3;
    public DateTime? LastResetTime { get; set; }

    // 1. Kapanma Koruması ("Otomatik Aç")
    public bool AutoRestartOnUnexpectedExit { get; set; } = true;

    // 2. CPU Eşik Takipli Resetleme (Native Kapalı)
    public bool CpuWatchdogEnabled { get; set; } = false;
    public double CpuThresholdPercent { get; set; } = 95.0; // %95 ve üzeri
    public int CpuSustainedMinutes { get; set; } = 15;      // 15 dakika boyunca

    // 3. RAM Eşik Takipli Resetleme (Native Kapalı)
    public bool RamWatchdogEnabled { get; set; } = false;
    public double RamThresholdMb { get; set; } = 1024.0;    // 1024 MB ve üzeri
    public int RamSustainedMinutes { get; set; } = 15;      // 15 dakika boyunca
}

public class ResetLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TriggerType { get; set; } = "Scheduled"; // Scheduled, Manual, UnexpectedExitWatchdog, CpuThresholdExceeded, RamThresholdExceeded, SetupAutoStart
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PreviousPid { get; set; }
    public int NewPid { get; set; }
}
