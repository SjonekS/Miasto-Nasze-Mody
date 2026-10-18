namespace AmongUsModManager.Models;

public class LocalState
{
    /// <summary>
    /// Currently installed and verified mod information.
    /// NULL if no mod has been installed yet by the manager.
    /// </summary>
    public InstalledModInfo? InstalledMod { get; set; }

    /// <summary>
    /// Existing backup archives.
    /// </summary>
    public List<BackupEntry> Backups { get; set; } = new();
}
