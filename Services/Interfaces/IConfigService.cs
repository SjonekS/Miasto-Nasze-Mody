using AmongUsModManager.Models;

namespace AmongUsModManager.Services.Interfaces;

public interface IConfigService
{
    AppConfig Config { get; }
    LocalState State { get; }
    void Load();
    void SaveConfig();
    void SaveState();
    string GetAppDataDirectory();
    string GetBackupsDirectory();
}
