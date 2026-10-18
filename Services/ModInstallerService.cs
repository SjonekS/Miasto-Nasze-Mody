using System.IO;
using System.IO.Compression;
using AmongUsModManager.Models;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public class ModInstallerService : IModInstallerService
{
    private readonly IConfigService _configService;
    private readonly ISteamLocatorService _steamLocator;
    private readonly IGamePathValidator _pathValidator;
    private readonly IProcessChecker _processChecker;
    private readonly IBackupService _backupService;
    private readonly IModCleanupService _modCleanup;

    public ModInstallerService(
        IConfigService configService,
        ISteamLocatorService steamLocator,
        IGamePathValidator pathValidator,
        IProcessChecker processChecker,
        IBackupService backupService,
        IModCleanupService modCleanup)
    {
        _configService = configService;
        _steamLocator = steamLocator;
        _pathValidator = pathValidator;
        _processChecker = processChecker;
        _backupService = backupService;
        _modCleanup = modCleanup;
    }

    public VersionComparison CompareWithInstalled(ModPackageInfo package, string? customInstalledVersionString = null)
    {
        if (customInstalledVersionString != null)
        {
            if (string.IsNullOrWhiteSpace(customInstalledVersionString))
                return VersionComparison.FirstInstall;

            if (Version.TryParse(customInstalledVersionString.TrimStart('v', 'V'), out var parsedVer))
            {
                if (package.Version > parsedVer) return VersionComparison.NewerVersion;
                if (package.Version < parsedVer) return VersionComparison.OlderVersion;
                return VersionComparison.SameVersion;
            }

            return VersionComparison.FirstInstall;
        }

        var installed = _configService.State.InstalledMod;
        if (installed == null)
        {
            return VersionComparison.FirstInstall;
        }

        var installedVer = installed.AsVersion();
        if (package.Version > installedVer) return VersionComparison.NewerVersion;
        if (package.Version < installedVer) return VersionComparison.OlderVersion;
        return VersionComparison.SameVersion;
    }

    public string? ResolveGamePath()
    {
        if (!string.IsNullOrWhiteSpace(_configService.Config.CustomGamePath))
        {
            var res = _pathValidator.Validate(_configService.Config.CustomGamePath);
            if (res.IsValid)
            {
                return _configService.Config.CustomGamePath;
            }
        }

        var steamPath = _steamLocator.FindAmongUsSteamPath();
        if (!string.IsNullOrEmpty(steamPath))
        {
            return steamPath;
        }

        return _configService.Config.CustomGamePath;
    }

    public ValidationResult ValidateCurrentGamePath()
    {
        var path = ResolveGamePath();
        return _pathValidator.Validate(path);
    }

    public async Task<bool> InstallPackageAsync(ModPackageInfo package, string targetGamePath, IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
    {
        // 1. Check if game is running
        if (_processChecker.IsGameRunning())
        {
            throw new InvalidOperationException("Gra Among Us jest uruchomiona! Zamknij grę przed instalacją modów.");
        }

        // 2. Validate game directory
        var validation = _pathValidator.Validate(targetGamePath);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Nieprawidłowy folder gry:\n{validation.Message}");
        }

        // 3. Verify ZIP file
        if (!File.Exists(package.ZipFilePath))
        {
            throw new FileNotFoundException($"Nie znaleziono pliku ZIP:\n{package.ZipFilePath}");
        }

        // 4. Create full backup of game folder before modifying anything
        if (_configService.Config.AutoBackupBeforeUpdate)
        {
            progress?.Report(new OperationProgress(5, "Tworzenie pełnej kopii zapasowej gry (ZIP backup)..."));
            var backupProg = new Progress<double>(p =>
            {
                progress?.Report(new OperationProgress(5 + (p * 0.25), $"Tworzenie kopii zapasowej ({p:0}%)..."));
            });

            await _backupService.CreateBackupAsync(targetGamePath, backupProg, ct);
        }

        // 4.5 Process cleanup directives from the ZIP (or local manifest)
        // This removes files listed in cleanup.txt before new files are extracted,
        // preventing orphaned plugin DLLs when a mod renames/removes plugins between versions.
        progress?.Report(new OperationProgress(33, "Przetwarzanie dyrektyw czyszczenia plików..."));
        await _modCleanup.ProcessCleanupDirectivesAsync(
            package.ZipFilePath,
            targetGamePath,
            log => System.Diagnostics.Debug.WriteLine(log));

        // 5. Extract ZIP safely

        progress?.Report(new OperationProgress(35, "Przygotowanie do rozpakowywania paczki moda..."));

        await Task.Run(() =>
        {
            using var zip = ZipFile.OpenRead(package.ZipFilePath);
            var entries = zip.Entries.ToList();
            if (entries.Count == 0)
            {
                throw new InvalidOperationException("Pobrany plik ZIP jest pusty.");
            }

            string rootPrefix = package.PayloadRootPrefix;

            int totalEntries = entries.Count;
            for (int i = 0; i < totalEntries; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = entries[i];
                var normalizedFullName = entry.FullName.Replace('\\', '/');

                // If root prefix was detected, only extract items inside or strip the prefix
                if (!string.IsNullOrEmpty(rootPrefix) && !normalizedFullName.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Ignore top-level metadata or non-payload items outside root
                    continue;
                }

                // Strip container root prefix
                string relativePath = !string.IsNullOrEmpty(rootPrefix) && normalizedFullName.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? normalizedFullName.Substring(rootPrefix.Length)
                    : normalizedFullName;

                if (string.IsNullOrWhiteSpace(relativePath) || relativePath.EndsWith("/"))
                {
                    // Directory entry
                    continue;
                }

                // Security check: prevent Zip Slip vulnerability
                var destinationPath = Path.GetFullPath(Path.Combine(targetGamePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                var targetGameFullPath = Path.GetFullPath(targetGamePath);

                if (!destinationPath.StartsWith(targetGameFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Wykryto niebezpieczną ścieżkę w archiwum ZIP: {relativePath}");
                }

                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // Remove readonly if file exists
                if (File.Exists(destinationPath))
                {
                    var attrs = File.GetAttributes(destinationPath);
                    if (attrs.HasFlag(FileAttributes.ReadOnly))
                    {
                        File.SetAttributes(destinationPath, attrs & ~FileAttributes.ReadOnly);
                    }
                }

                entry.ExtractToFile(destinationPath, overwrite: true);

                double percent = 35 + ((double)(i + 1) / totalEntries * 55.0);
                progress?.Report(new OperationProgress(percent, $"Rozpakowywanie ({i + 1}/{totalEntries}): {Path.GetFileName(destinationPath)}"));
            }
        }, ct);

        // 6. Post-install verification
        progress?.Report(new OperationProgress(95, "Weryfikacja poprawności instalacji..."));

        var bepInExPath = Path.Combine(targetGamePath, "BepInEx");
        var dotnetPath = Path.Combine(targetGamePath, "dotnet");
        var doorstopIni = Path.Combine(targetGamePath, "doorstop_config.ini");
        var winhttpDll = Path.Combine(targetGamePath, "winhttp.dll");

        bool verificationPassed = Directory.Exists(bepInExPath) ||
                                 Directory.Exists(dotnetPath) ||
                                 File.Exists(doorstopIni) ||
                                 File.Exists(winhttpDll);

        if (!verificationPassed)
        {
            throw new InvalidOperationException("Weryfikacja po instalacji nie powiodła się: w folderze gry nie odnaleziono plików modyfikacji (BepInEx / dotnet / doorstop).");
        }

        // 7. Save installed state ONLY upon complete verification
        var instanceJsonPath = Path.Combine(targetGamePath, "manager-data", "instance.json");
        if (File.Exists(instanceJsonPath))
        {
            try
            {
                var json = File.ReadAllText(instanceJsonPath);
                var meta = System.Text.Json.JsonSerializer.Deserialize<InstanceMetadata>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new InstanceMetadata();
                meta.InstalledVersion = package.RawVersionString;
                meta.LastModInstalledAt = DateTimeOffset.Now;
                var updatedJson = System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(instanceJsonPath, updatedJson);
            }
            catch { }
        }
        else
        {
            _configService.State.InstalledMod = new InstalledModInfo
            {
                ModName = package.ModName,
                VersionString = package.RawVersionString,
                Major = package.Version.Major,
                Minor = package.Version.Minor,
                Patch = package.Version.Build >= 0 ? package.Version.Build : 0,
                Architecture = package.Architecture,
                Variant = package.Variant,
                InstalledAt = DateTimeOffset.Now
            };
            _configService.SaveState();
        }

        progress?.Report(new OperationProgress(100, $"Pomyślnie zainstalowano {package.ModName} {package.RawVersionString}!"));
        return true;
    }
}
