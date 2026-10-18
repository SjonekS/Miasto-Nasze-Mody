namespace AmongUsModManager.Services.Interfaces;

/// <summary>
/// Service responsible for cleanup directives (deleting specific files before mod extraction)
/// and quick clearing of the BepInEx/plugins folder.
/// </summary>
public interface IModCleanupService
{
    /// <summary>
    /// Reads cleanup.txt (or equivalent) from the given ZIP and deletes the listed files
    /// from the target game directory. Returns the count of files successfully removed.
    /// </summary>
    Task<int> ProcessCleanupDirectivesAsync(string zipFilePath, string targetGamePath, Action<string>? log = null);

    /// <summary>
    /// Deletes all files inside BepInEx/plugins without touching config, core, or any game files.
    /// Returns true on success.
    /// </summary>
    Task<bool> QuickCleanPluginsFolderAsync(string gamePath, Action<string>? log = null);
}
