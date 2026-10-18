using System.IO;
using System.Text.Json;
using AmongUsModManager.Models;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseDir;
    private readonly string _dataDir;
    private readonly string _backupsDir;
    private readonly string _configFilePath;
    private readonly string _stateFilePath;

    public AppConfig Config { get; private set; } = new();
    public LocalState State { get; private set; } = new();

    public ConfigService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _baseDir = Path.Combine(localAppData, "AmongUsModManager");
        _dataDir = Path.Combine(_baseDir, "data");
        _backupsDir = Path.Combine(_baseDir, "backups");
        _configFilePath = Path.Combine(_dataDir, "config.json");
        _stateFilePath = Path.Combine(_dataDir, "state.json");

        MigrateLegacyData();
        EnsureDirectories();
        Load();
    }

    private void MigrateLegacyData()
    {
        try
        {
            var oldBaseDir = AppContext.BaseDirectory;
            var oldDataDir = Path.Combine(oldBaseDir, "data");
            var oldBackupsDir = Path.Combine(oldBaseDir, "backups");

            // Jeśli istnieje stary folder data i nowy jeszcze nie ma plików, migrujemy
            if (Directory.Exists(oldDataDir) && !Directory.Exists(_dataDir))
            {
                Directory.CreateDirectory(_dataDir);
                foreach (var file in Directory.GetFiles(oldDataDir, "*.*"))
                {
                    var dest = Path.Combine(_dataDir, Path.GetFileName(file));
                    if (!File.Exists(dest)) File.Copy(file, dest, overwrite: true);
                }
            }

            // Migracja backupów
            if (Directory.Exists(oldBackupsDir) && !Directory.Exists(_backupsDir))
            {
                Directory.CreateDirectory(_backupsDir);
                foreach (var file in Directory.GetFiles(oldBackupsDir, "*.zip"))
                {
                    var dest = Path.Combine(_backupsDir, Path.GetFileName(file));
                    if (!File.Exists(dest)) File.Copy(file, dest, overwrite: true);
                }
            }
        }
        catch
        {
            // Migracja nie może zablokować działania aplikacji
        }
    }

    private void EnsureDirectories()
    {
        if (!Directory.Exists(_baseDir)) Directory.CreateDirectory(_baseDir);
        if (!Directory.Exists(_dataDir)) Directory.CreateDirectory(_dataDir);
        if (!Directory.Exists(_backupsDir)) Directory.CreateDirectory(_backupsDir);
    }

    public string GetAppDataDirectory() => _dataDir;
    public string GetBackupsDirectory() => _backupsDir;

    public void Load()
    {
        EnsureDirectories();

        // Load Config
        if (File.Exists(_configFilePath))
        {
            try
            {
                var json = File.ReadAllText(_configFilePath);
                Config = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
            }
            catch
            {
                Config = new AppConfig();
            }
        }
        else
        {
            Config = new AppConfig();
            SaveConfig();
        }

        // Load State
        if (File.Exists(_stateFilePath))
        {
            try
            {
                var json = File.ReadAllText(_stateFilePath);
                State = JsonSerializer.Deserialize<LocalState>(json, JsonOpts) ?? new LocalState();
            }
            catch
            {
                State = new LocalState();
            }
        }
        else
        {
            State = new LocalState();
            SaveState();
        }
    }

    public void SaveConfig()
    {
        EnsureDirectories();
        var json = JsonSerializer.Serialize(Config, JsonOpts);
        File.WriteAllText(_configFilePath, json);
    }

    public void SaveState()
    {
        EnsureDirectories();
        var json = JsonSerializer.Serialize(State, JsonOpts);
        File.WriteAllText(_stateFilePath, json);
    }
}
