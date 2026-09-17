using System.Text.Json;
using RadioTEDU.AutoReset.Models;

namespace RadioTEDU.AutoReset.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _baseDirectory;
    private readonly string _configFilePath;
    private readonly string _historyFilePath;

    public ConfigService()
    {
        // Use directory of executing assembly / exe
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        if (IsDirectoryWritable(exeDir))
        {
            _baseDirectory = exeDir;
        }
        else
        {
            _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadioTEDU.AutoReset");
            Directory.CreateDirectory(_baseDirectory);
        }

        _configFilePath = Path.Combine(_baseDirectory, "config.json");
        _historyFilePath = Path.Combine(_baseDirectory, "reset_history.json");
    }

    public string ConfigPath => _configFilePath;

    public bool HasConfig() => File.Exists(_configFilePath);

    public ResetConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var cfg = JsonSerializer.Deserialize<ResetConfig>(json, JsonOptions);
                if (cfg != null) return cfg;
            }
        }
        catch { }

        return new ResetConfig();
    }

    public void SaveConfig(ResetConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config Hata] Ayarlar kaydedilemedi: {ex.Message}");
        }
    }

    public List<ResetLogEntry> LoadHistory()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = File.ReadAllText(_historyFilePath);
                var logs = JsonSerializer.Deserialize<List<ResetLogEntry>>(json, JsonOptions);
                if (logs != null) return logs;
            }
        }
        catch { }

        return new List<ResetLogEntry>();
    }

    public void AppendHistory(ResetLogEntry entry)
    {
        try
        {
            var history = LoadHistory();
            history.Insert(0, entry); // Most recent first
            if (history.Count > 100)
            {
                history = history.Take(100).ToList();
            }

            var json = JsonSerializer.Serialize(history, JsonOptions);
            File.WriteAllText(_historyFilePath, json);
        }
        catch { }
    }

    public bool FactoryReset()
    {
        try
        {
            if (File.Exists(_configFilePath)) File.Delete(_configFilePath);
            if (File.Exists(_historyFilePath)) File.Delete(_historyFilePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDirectoryWritable(string path)
    {
        try
        {
            var testFile = Path.Combine(path, Path.GetRandomFileName());
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
