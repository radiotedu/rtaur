using System.Diagnostics;
using RadioTEDU.AutoReset.Models;

namespace RadioTEDU.AutoReset.Services;

public class TargetProcessStatus
{
    public string ExePath { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int Pid { get; set; }
    public double MemoryMb { get; set; }
    public double CpuPercent { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class ProcessManager
{
    public static TargetProcessStatus GetStatus(string targetExePath)
    {
        if (string.IsNullOrWhiteSpace(targetExePath) || !File.Exists(targetExePath))
        {
            return new TargetProcessStatus { ExePath = targetExePath, IsRunning = false };
        }

        var procName = Path.GetFileNameWithoutExtension(targetExePath);
        var procs = Process.GetProcessesByName(procName);

        foreach (var p in procs)
        {
            try
            {
                bool matches = false;
                try
                {
                    if (string.Equals(p.MainModule?.FileName, targetExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                    }
                }
                catch
                {
                    matches = true;
                }

                if (matches && !p.HasExited)
                {
                    p.Refresh();
                    var memMb = p.WorkingSet64 / (1024.0 * 1024.0);
                    var uptime = DateTime.Now - p.StartTime;
                    return new TargetProcessStatus
                    {
                        ExePath = targetExePath,
                        ProcessName = procName,
                        IsRunning = true,
                        Pid = p.Id,
                        MemoryMb = Math.Round(memMb, 1),
                        Uptime = uptime
                    };
                }
            }
            catch { }
        }

        return new TargetProcessStatus { ExePath = targetExePath, IsRunning = false, ProcessName = procName };
    }

    public static List<TargetProcessStatus> GetStatuses(IEnumerable<WatchedApp> apps)
    {
        var list = new List<TargetProcessStatus>();
        foreach (var app in apps)
        {
            if (app.Enabled)
            {
                var status = GetStatus(app.ExePath);
                list.Add(status);
            }
        }
        return list;
    }

    public static async Task<(bool Success, string Message, int OldPid, int NewPid)> RestartTargetAsync(string targetExePath, int gracefulWaitSeconds = 3)
    {
        if (string.IsNullOrWhiteSpace(targetExePath) || !File.Exists(targetExePath))
        {
            return (false, $"Hedef dosya bulunamadı: {targetExePath}", 0, 0);
        }

        int oldPid = 0;
        var procName = Path.GetFileNameWithoutExtension(targetExePath);
        var existingProcs = Process.GetProcessesByName(procName);

        // 1. Kill existing processes
        foreach (var p in existingProcs)
        {
            try
            {
                oldPid = p.Id;
                try
                {
                    p.CloseMainWindow();
                }
                catch { }

                await Task.Delay(1000);
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                }
            }
            catch (Exception)
            {
                // Process might already be dead
            }
        }

        // Wait for sockets/ports and file handles to free up
        await Task.Delay(Math.Max(1, gracefulWaitSeconds) * 1000);

        // 2. Start target process
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = targetExePath,
                WorkingDirectory = Path.GetDirectoryName(targetExePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = true
            };

            var newProc = Process.Start(psi);
            if (newProc != null)
            {
                int newPid = 0;
                try { newPid = newProc.Id; } catch { }
                return (true, $"{Path.GetFileName(targetExePath)} yeniden başlatıldı (Yeni PID: {newPid})", oldPid, newPid);
            }
            else
            {
                return (false, "İşlem başlatılamadı (Process.Start null döndü)", oldPid, 0);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Yeniden başlatma hatası: {ex.Message}", oldPid, 0);
        }
    }

    public static async Task<List<(string AppName, bool Success, string Message, int OldPid, int NewPid)>> RestartAllAppsAsync(IEnumerable<WatchedApp> apps, int gracefulWaitSeconds = 3)
    {
        var results = new List<(string AppName, bool Success, string Message, int OldPid, int NewPid)>();
        foreach (var app in apps.Where(a => a.Enabled))
        {
            var res = await RestartTargetAsync(app.ExePath, gracefulWaitSeconds);
            results.Add((app.Name, res.Success, res.Message, res.OldPid, res.NewPid));
        }
        return results;
    }

    public static async Task<(bool Started, string Message, int Pid)> EnsureRunningAsync(string targetExePath)
    {
        if (string.IsNullOrWhiteSpace(targetExePath) || !File.Exists(targetExePath))
        {
            return (false, "Geçersiz veya bulunamayan dosya yolu", 0);
        }

        var status = GetStatus(targetExePath);
        if (status.IsRunning)
        {
            return (false, "Uygulama zaten çalışıyor", status.Pid);
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = targetExePath,
                WorkingDirectory = Path.GetDirectoryName(targetExePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = true
            };

            var proc = Process.Start(psi);
            if (proc != null)
            {
                await Task.Delay(500);
                return (true, $"{Path.GetFileName(targetExePath)} başarıyla başlatıldı", proc.Id);
            }
        }
        catch (Exception ex)
        {
            return (false, $"Başlatma hatası: {ex.Message}", 0);
        }

        return (false, "Uygulama başlatılamadı", 0);
    }

    public static async Task<List<(string AppName, bool Started, string Message, int Pid)>> EnsureAllRunningAsync(IEnumerable<WatchedApp> apps)
    {
        var results = new List<(string AppName, bool Started, string Message, int Pid)>();
        foreach (var app in apps.Where(a => a.Enabled))
        {
            var res = await EnsureRunningAsync(app.ExePath);
            results.Add((app.Name, res.Started, res.Message, res.Pid));
        }
        return results;
    }
}
