using System.IO;
using System.IO.Compression;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

/// <summary>
/// Implements cleanup directive processing and quick-plugin-folder clearing.
/// Safety guarantees:
///   - Never deletes files outside targetGamePath (path traversal check).
///   - Never deletes protected items: Among Us.exe, Among Us_Data/, BepInEx/config/,
///     BepInEx/core/, backups/, manager-data/.
///   - Never touches system directories.
/// </summary>
public class ModCleanupService : IModCleanupService
{
    // Names of cleanup manifest files accepted inside the ZIP (case-insensitive)
    private static readonly string[] CleanupFileNames =
        ["cleanup.txt", "delete.txt", "delete_list.txt", ".cleanup"];

    // Protected relative paths (compared case-insensitively after normalisation to the game root)
    private static readonly string[] ProtectedRelPaths =
    [
        "among us.exe",
        "among us_data",
        "bepinex/config",
        "bepinex/core",
        "backups",
        "manager-data"
    ];

    // ────────────────────────────────────────────────────────────────────────
    public async Task<int> ProcessCleanupDirectivesAsync(
        string zipFilePath, string targetGamePath, Action<string>? log = null)
    {
        if (!File.Exists(zipFilePath)) return 0;
        if (!Directory.Exists(targetGamePath)) return 0;

        var gameRoot = Path.GetFullPath(targetGamePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Also check for a local override manifest next to the manager-data folder
        var localRulesPath = Path.Combine(gameRoot, "manager-data", "cleanup_rules.txt");

        var linesToDelete = new List<string>();

        // 1. Try to read from ZIP
        await Task.Run(() =>
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipFilePath);
                var cleanupEntry = zip.Entries.FirstOrDefault(e =>
                    CleanupFileNames.Contains(
                        Path.GetFileName(e.FullName).ToLowerInvariant()));

                if (cleanupEntry != null)
                {
                    using var reader = new StreamReader(cleanupEntry.Open());
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                        linesToDelete.Add(line);

                    log?.Invoke($"[Cleanup] Znaleziono manifest usuwania w ZIP: {cleanupEntry.FullName} ({linesToDelete.Count} linii).");
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Cleanup] Błąd odczytu ZIP dla manifestu: {ex.Message}");
            }
        });

        // 2. Merge with local manifest if it exists
        if (File.Exists(localRulesPath))
        {
            var localLines = await File.ReadAllLinesAsync(localRulesPath);
            linesToDelete.AddRange(localLines);
            log?.Invoke($"[Cleanup] Lokalny manifest cleanup_rules.txt zawiera {localLines.Length} linii.");
        }

        if (linesToDelete.Count == 0) return 0;

        int deleted = 0;
        await Task.Run(() =>
        {
            foreach (var raw in linesToDelete)
            {
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

                // Normalise path separators
                var normalised = trimmed.Replace('\\', '/');

                // If no directory separator — treat as a bare filename inside BepInEx/plugins/
                string relPath = normalised.Contains('/')
                    ? normalised
                    : $"BepInEx/plugins/{normalised}";

                // Safety: resolve full path and ensure it's inside game root
                string fullTarget;
                try
                {
                    fullTarget = Path.GetFullPath(
                        Path.Combine(gameRoot, relPath.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch
                {
                    log?.Invoke($"[Cleanup] Odrzucono nieprawidłową ścieżkę: {trimmed}");
                    continue;
                }

                if (!fullTarget.StartsWith(gameRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(fullTarget, gameRoot, StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke($"[Cleanup] Odrzucono próbę wyjścia poza folder gry: {trimmed}");
                    continue;
                }

                // Safety: protected paths
                if (IsProtected(fullTarget, gameRoot))
                {
                    log?.Invoke($"[Cleanup] Chroniony plik/folder — pominięto: {trimmed}");
                    continue;
                }

                try
                {
                    if (File.Exists(fullTarget))
                    {
                        File.Delete(fullTarget);
                        deleted++;
                        log?.Invoke($"[Cleanup] Usunięto plik: {relPath}");
                    }
                    else if (Directory.Exists(fullTarget))
                    {
                        Directory.Delete(fullTarget, true);
                        deleted++;
                        log?.Invoke($"[Cleanup] Usunięto folder: {relPath}");
                    }
                    else
                    {
                        log?.Invoke($"[Cleanup] Nie znaleziono (pominięto): {relPath}");
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[Cleanup] Błąd przy usuwaniu '{relPath}': {ex.Message}");
                }
            }
        });

        log?.Invoke($"[Cleanup] Zakończono przetwarzanie dyrektyw — usunięto {deleted} elementów.");
        return deleted;
    }

    // ────────────────────────────────────────────────────────────────────────
    public async Task<bool> QuickCleanPluginsFolderAsync(string gamePath, Action<string>? log = null)
    {
        if (!Directory.Exists(gamePath)) return false;

        var gameRoot = Path.GetFullPath(gamePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var pluginsDir = Path.Combine(gameRoot, "BepInEx", "plugins");
        if (!Directory.Exists(pluginsDir))
        {
            log?.Invoke("[QuickClean] Folder BepInEx/plugins nie istnieje — brak działania.");
            return true; // nothing to clean, still success
        }

        int removed = 0;
        await Task.Run(() =>
        {
            // Delete all files at top level and in subdirectories
            foreach (var file in Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[QuickClean] Nie można usunąć pliku '{file}': {ex.Message}");
                }
            }

            // Remove now-empty subdirectories
            foreach (var dir in Directory.GetDirectories(pluginsDir, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"[QuickClean] Nie można usunąć podfolderu '{dir}': {ex.Message}");
                }
            }
        });

        log?.Invoke($"[QuickClean] Wyczyszczono folder plugins — usunięto {removed} plików.");
        return true;
    }

    // ────────────────────────────────────────────────────────────────────────
    private static bool IsProtected(string fullTarget, string gameRoot)
    {
        // Build the relative path from game root (lowercase, forward-slash normalised)
        var rel = fullTarget.Length > gameRoot.Length
            ? fullTarget.Substring(gameRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : string.Empty;

        rel = rel.Replace('\\', '/').ToLowerInvariant();

        foreach (var prot in ProtectedRelPaths)
        {
            if (rel == prot || rel.StartsWith(prot + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Also block if target IS the game root itself
        if (string.Equals(fullTarget, gameRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
