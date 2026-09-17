using System.Collections.Concurrent;
using System.Diagnostics;
using RadioTEDU.AutoReset.Models;

namespace RadioTEDU.AutoReset.Services;

public class ResourceBreachEventArgs : EventArgs
{
    public string ExePath { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty; // CpuThresholdExceeded or RamThresholdExceeded
    public double Value { get; set; }
    public int SustainedMinutes { get; set; }
}

public class AppResourceMetric
{
    public string ExePath { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double RamMb { get; set; }
    public TimeSpan? CpuBreachDuration { get; set; }
    public TimeSpan? RamBreachDuration { get; set; }
}

public class ResourceMonitorService
{
    private readonly ConcurrentDictionary<string, DateTime> _cpuBreaches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _ramBreaches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (int Pid, TimeSpan CpuTime, DateTime SampleTime)> _cpuSamples = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, AppResourceMetric> _latestMetrics = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ResourceBreachEventArgs>? OnBreachThresholdExceeded;

    public IReadOnlyDictionary<string, AppResourceMetric> LatestMetrics => _latestMetrics;

    public void SampleAndCheck(IEnumerable<WatchedApp> apps, ResetConfig config)
    {
        var activeApps = apps.Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.ExePath) && File.Exists(a.ExePath)).ToList();

        foreach (var app in activeApps)
        {
            SampleApp(app, config);
        }
    }

    private void SampleApp(WatchedApp app, ResetConfig config)
    {
        var procName = Path.GetFileNameWithoutExtension(app.ExePath);
        var procs = Process.GetProcessesByName(procName);
        Process? targetProc = null;

        foreach (var p in procs)
        {
            try
            {
                if (!p.HasExited)
                {
                    targetProc = p;
                    break;
                }
            }
            catch { }
        }

        if (targetProc == null)
        {
            _cpuBreaches.TryRemove(app.ExePath, out _);
            _ramBreaches.TryRemove(app.ExePath, out _);
            _cpuSamples.TryRemove(app.ExePath, out _);
            _latestMetrics[app.ExePath] = new AppResourceMetric { ExePath = app.ExePath, AppName = app.Name, CpuPercent = 0, RamMb = 0 };
            return;
        }

        try
        {
            targetProc.Refresh();
            var ramMb = Math.Round(targetProc.WorkingSet64 / (1024.0 * 1024.0), 1);
            var nowUtc = DateTime.UtcNow;
            var currentCpuTime = targetProc.TotalProcessorTime;
            double cpuPercent = 0.0;

            if (_cpuSamples.TryGetValue(app.ExePath, out var prevSample) && prevSample.Pid == targetProc.Id)
            {
                var elapsedMs = (nowUtc - prevSample.SampleTime).TotalMilliseconds;
                var cpuUsedMs = (currentCpuTime - prevSample.CpuTime).TotalMilliseconds;

                if (elapsedMs > 500)
                {
                    var calcCpu = (cpuUsedMs / (elapsedMs * Environment.ProcessorCount)) * 100.0;
                    cpuPercent = Math.Clamp(Math.Round(calcCpu, 1), 0.0, 100.0);
                    _cpuSamples[app.ExePath] = (targetProc.Id, currentCpuTime, nowUtc);
                }
            }
            else
            {
                _cpuSamples[app.ExePath] = (targetProc.Id, currentCpuTime, nowUtc);
            }

            // 1. CPU Breach Check
            TimeSpan? cpuBreachDur = null;
            if (config.CpuWatchdogEnabled)
            {
                if (cpuPercent >= config.CpuThresholdPercent)
                {
                    var breachStart = _cpuBreaches.GetOrAdd(app.ExePath, DateTime.Now);
                    cpuBreachDur = DateTime.Now - breachStart;

                    if (cpuBreachDur.Value.TotalMinutes >= config.CpuSustainedMinutes)
                    {
                        _cpuBreaches.TryRemove(app.ExePath, out _);
                        OnBreachThresholdExceeded?.Invoke(this, new ResourceBreachEventArgs
                        {
                            ExePath = app.ExePath,
                            AppName = app.Name,
                            TriggerType = "CpuThresholdExceeded",
                            Reason = $"'{app.Name}' aşırı CPU kullanımı (%{cpuPercent:F1}) {config.CpuSustainedMinutes} dakika boyunca devam ettiği için otomatik resetlendi.",
                            Value = cpuPercent,
                            SustainedMinutes = config.CpuSustainedMinutes
                        });
                    }
                }
                else
                {
                    _cpuBreaches.TryRemove(app.ExePath, out _);
                }
            }
            else
            {
                _cpuBreaches.TryRemove(app.ExePath, out _);
            }

            // 2. RAM Breach Check
            TimeSpan? ramBreachDur = null;
            if (config.RamWatchdogEnabled)
            {
                if (ramMb >= config.RamThresholdMb)
                {
                    var breachStart = _ramBreaches.GetOrAdd(app.ExePath, DateTime.Now);
                    ramBreachDur = DateTime.Now - breachStart;

                    if (ramBreachDur.Value.TotalMinutes >= config.RamSustainedMinutes)
                    {
                        _ramBreaches.TryRemove(app.ExePath, out _);
                        OnBreachThresholdExceeded?.Invoke(this, new ResourceBreachEventArgs
                        {
                            ExePath = app.ExePath,
                            AppName = app.Name,
                            TriggerType = "RamThresholdExceeded",
                            Reason = $"'{app.Name}' aşırı RAM kullanımı ({ramMb:F0} MB >= {config.RamThresholdMb:F0} MB) {config.RamSustainedMinutes} dakika boyunca devam ettiği için otomatik resetlendi.",
                            Value = ramMb,
                            SustainedMinutes = config.RamSustainedMinutes
                        });
                    }
                }
                else
                {
                    _ramBreaches.TryRemove(app.ExePath, out _);
                }
            }
            else
            {
                _ramBreaches.TryRemove(app.ExePath, out _);
            }

            _latestMetrics[app.ExePath] = new AppResourceMetric
            {
                ExePath = app.ExePath,
                AppName = app.Name,
                CpuPercent = cpuPercent,
                RamMb = ramMb,
                CpuBreachDuration = cpuBreachDur,
                RamBreachDuration = ramBreachDur
            };
        }
        catch
        {
            // Process may have exited during sampling
        }
    }

    public void ResetBreachTimers()
    {
        _cpuBreaches.Clear();
        _ramBreaches.Clear();
    }
}
