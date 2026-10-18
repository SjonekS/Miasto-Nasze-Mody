namespace AmongUsModManager.Models;

public class AppConfig
{
    /// <summary>
    /// Default web URL to the TouMira mod download folder on Google Drive.
    /// </summary>
    public string DownloadFolderUrl { get; set; } = "https://drive.google.com/drive/folders/1GQkmcLMM4UIEnFH1yaeVDo94-pPPnTOx";

    /// <summary>
    /// Custom game directory if not using auto-detected Steam path.
    /// </summary>
    public string? CustomGamePath { get; set; }

    /// <summary>
    /// Whether to automatically create a backup before updating.
    /// </summary>
    public bool AutoBackupBeforeUpdate { get; set; } = true;

    /// <summary>
    /// Maximum number of backups to keep in rotation (default: 2).
    /// </summary>
    public int MaxBackups { get; set; } = 2;

    /// <summary>
    /// Currently selected color theme (SteelBlue, Emerald, Amber, Titanium).
    /// </summary>
    public string SelectedTheme { get; set; } = "SteelBlue";
}
