using System.IO;
using System.Text.Json;
using AmongUsModManager.Models;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public class InstanceManagerService : IInstanceManagerService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IConfigService _configService;
    private readonly IGamePathValidator _gamePathValidator;
    private readonly IProcessChecker _processChecker;
    private readonly string _registryFilePath;

    public InstanceManagerService(
        IConfigService configService,
        IGamePathValidator gamePathValidator,
        IProcessChecker processChecker)
    {
        _configService = configService;
        _gamePathValidator = gamePathValidator;
        _processChecker = processChecker;
        _registryFilePath = Path.Combine(_configService.GetAppDataDirectory(), "instances.json");
    }

    private async Task<List<string>> LoadRegistryPathsAsync()
    {
        if (!File.Exists(_registryFilePath))
            return new List<string>();

        try
        {
            var json = await File.ReadAllTextAsync(_registryFilePath);
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task SaveRegistryPathsAsync(IEnumerable<string> paths)
    {
        var dir = Path.GetDirectoryName(_registryFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var distinctList = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var json = JsonSerializer.Serialize(distinctList, JsonOpts);
        await File.WriteAllTextAsync(_registryFilePath, json);
    }

    public async Task<List<InstanceItem>> GetAllInstancesAsync()
    {
        var paths = await LoadRegistryPathsAsync();
        var validPaths = new List<string>();
        var result = new List<InstanceItem>();

        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                var meta = await ReadInstanceMetadataAsync(p);
                var exeExists = File.Exists(Path.Combine(p, "Among Us.exe"));
                
                var item = new InstanceItem
                {
                    Name = meta?.Name ?? Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    Path = p,
                    GameVersion = _gamePathValidator.DetectGameVersion(p),
                    InstalledVersion = meta?.InstalledVersion,
                    CreatedAt = meta?.CreatedAt ?? (Directory.GetCreationTime(p)),
                    LastModInstalledAt = meta?.LastModInstalledAt,
                    IsValid = exeExists && meta != null,
                    StatusText = (exeExists && meta != null) ? "Poprawna instancja" : "Wykryto brakujące pliki"
                };

                result.Add(item);
                validPaths.Add(p);
            }
        }

        // Zapisujemy tylko nadal istniejące ścieżki
        if (validPaths.Count != paths.Count)
        {
            await SaveRegistryPathsAsync(validPaths);
        }

        return result;
    }

    public ValidationResult ValidateNewInstanceLocation(string parentFolder, string instanceName, string sourceGamePath)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return new ValidationResult(false, "Nazwa instancji nie może być pusta.");

        var invalidChars = Path.GetInvalidFileNameChars();
        if (instanceName.Any(c => invalidChars.Contains(c)))
            return new ValidationResult(false, "Nazwa instancji zawiera niedozwolone znaki.");

        if (string.IsNullOrWhiteSpace(parentFolder) || !Directory.Exists(parentFolder))
            return new ValidationResult(false, "Wybrany folder docelowy nie istnieje.");

        var targetPath = Path.Combine(parentFolder, instanceName);
        var targetFullPath = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Zabezpieczenie przed nadpisaniem głównej gry
        if (!string.IsNullOrWhiteSpace(sourceGamePath))
        {
            var sourceFullPath = Path.GetFullPath(sourceGamePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(targetFullPath, sourceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(false, "Folder docelowy nie może być głównym folderem instalacji Among Us!");
            }

            if (targetFullPath.StartsWith(sourceFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(false, "Instancja nie może znajdować się wewnątrz głównego folderu gry Among Us!");
            }
        }

        // Zabezpieczenie przed folderami systemowymi i katalogiem aplikacji
        var appBase = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (targetFullPath.StartsWith(appBase, StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult(false, "Nie można tworzyć instancji wewnątrz folderu aplikacji Managera.");
        }

        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrEmpty(windir) && targetFullPath.StartsWith(Path.GetFullPath(windir), StringComparison.OrdinalIgnoreCase))
            return new ValidationResult(false, "Nie można tworzyć instancji w folderze systemowym Windows.");

        if (!string.IsNullOrEmpty(progFiles) && string.Equals(targetFullPath, Path.GetFullPath(progFiles), StringComparison.OrdinalIgnoreCase))
            return new ValidationResult(false, "Wybierz podfolder na instancję.");

        if (!string.IsNullOrEmpty(progFilesX86) && string.Equals(targetFullPath, Path.GetFullPath(progFilesX86), StringComparison.OrdinalIgnoreCase))
            return new ValidationResult(false, "Wybierz podfolder na instancję.");

        // Sprawdzenie czy folder docelowy już istnieje i ma pliki
        if (Directory.Exists(targetFullPath))
        {
            var files = Directory.GetFileSystemEntries(targetFullPath);
            if (files.Length > 0)
            {
                return new ValidationResult(false, $"Folder docelowy już istnieje i nie jest pusty: {targetFullPath}");
            }
        }

        return new ValidationResult(true, "Lokalizacja jest prawidłowa i bezpieczna.");
    }

    public async Task<InstanceItem> CreateInstanceAsync(
        string instanceName, 
        string targetDirectory, 
        string sourceGamePath, 
        IProgress<OperationProgress>? progress = null, 
        CancellationToken ct = default)
    {
        // 1. Walidacja
        var val = ValidateNewInstanceLocation(Path.GetDirectoryName(targetDirectory) ?? targetDirectory, instanceName, sourceGamePath);
        if (!val.IsValid)
        {
            throw new InvalidOperationException(val.Message);
        }

        var sourceVal = _gamePathValidator.Validate(sourceGamePath);
        if (!sourceVal.IsValid)
        {
            throw new InvalidOperationException($"Źródłowy folder gry Among Us jest nieprawidłowy: {sourceVal.Message}");
        }

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        // 2. Pobieramy listę plików do skopiowania ze źródła
        // Kopiujemy czyste pliki Among Us. Pomijamy wcześniejsze modyfikacje TouMira/BepInEx jeśli były w źródle,
        // aby instancja startowała jako czyste i stabilne środowisko!
        progress?.Report(new OperationProgress(5, "Przygotowywanie listy plików gry do skopiowania..."));

        var allFiles = Directory.GetFiles(sourceGamePath, "*.*", SearchOption.AllDirectories);
        var filesToCopy = new List<string>();

        foreach (var file in allFiles)
        {
            var relPath = Path.GetRelativePath(sourceGamePath, file);
            var firstSegment = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

            // Kopiujemy wszystkie pliki gry wraz z obecnymi modami ("kapsuła czasowa").
            // Pomijamy wyłącznie kopie zapasowe, metadane managera oraz pliki pobierania.
            if (firstSegment.Equals("backups", StringComparison.OrdinalIgnoreCase) ||
                firstSegment.Equals("manager-data", StringComparison.OrdinalIgnoreCase) ||
                relPath.EndsWith(".download", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filesToCopy.Add(file);
        }

        int total = filesToCopy.Count;
        if (total == 0)
        {
            throw new InvalidOperationException("Nie znaleziono plików gry w wybranym folderze źródłowym!");
        }

        // 3. Kopiowanie asynchroniczne z raportowaniem postępu
        await Task.Run(() =>
        {
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sourceFile = filesToCopy[i];
                var relPath = Path.GetRelativePath(sourceGamePath, sourceFile);
                var destFile = Path.Combine(targetDirectory, relPath);

                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(sourceFile, destFile, overwrite: true);

                double percent = 10.0 + ((double)(i + 1) / total * 80.0);
                progress?.Report(new OperationProgress(percent, $"Kopiowanie ({i + 1}/{total}): {Path.GetFileName(destFile)}"));
            }
        }, ct);

        // 4. Weryfikacja skopiowanej instancji
        progress?.Report(new OperationProgress(92, "Weryfikacja skopiowanych plików gry..."));
        var copiedExe = Path.Combine(targetDirectory, "Among Us.exe");
        if (!File.Exists(copiedExe))
        {
            throw new InvalidOperationException("Weryfikacja instancji nie powiodła się: plik Among Us.exe nie został poprawnie utworzony.");
        }

        // 5. Utworzenie metadanych instancji w manager-data/instance.json
        progress?.Report(new OperationProgress(96, "Tworzenie metadanych instancji..."));
        
        // Odczytujemy wersję modów ze źródła, aby zapisać ją w kapsule czasowej
        string? inheritedModVersion = _configService.State.InstalledMod?.VersionString;
        var meta = new InstanceMetadata
        {
            Name = instanceName,
            Path = targetDirectory,
            InstalledVersion = inheritedModVersion,
            CreatedAt = DateTimeOffset.Now,
            LastModInstalledAt = inheritedModVersion != null ? DateTimeOffset.Now : null
        };

        var metaDir = Path.Combine(targetDirectory, "manager-data");
        if (!Directory.Exists(metaDir))
        {
            Directory.CreateDirectory(metaDir);
        }

        var metaPath = Path.Combine(metaDir, "instance.json");
        var metaJson = JsonSerializer.Serialize(meta, JsonOpts);
        await File.WriteAllTextAsync(metaPath, metaJson, ct);

        // 6. Rejestracja w głównym rejestrze instances.json
        var registered = await LoadRegistryPathsAsync();
        registered.Add(targetDirectory);
        await SaveRegistryPathsAsync(registered);

        progress?.Report(new OperationProgress(100, $"Pomyślnie utworzono instancję {instanceName}!"));

        return new InstanceItem
        {
            Name = instanceName,
            Path = targetDirectory,
            GameVersion = _gamePathValidator.DetectGameVersion(targetDirectory),
            InstalledVersion = meta.InstalledVersion,
            CreatedAt = meta.CreatedAt,
            LastModInstalledAt = meta.LastModInstalledAt,
            IsValid = true,
            StatusText = meta.InstalledVersion != null 
                ? $"Kapsuła czasu (mod {meta.InstalledVersion})" 
                : "Czysta instalacja (brak modów)"
        };
    }

    public async Task UpdateInstanceModVersionAsync(string instancePath, string modVersion)
    {
        var meta = await ReadInstanceMetadataAsync(instancePath) ?? new InstanceMetadata
        {
            Name = Path.GetFileName(instancePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = instancePath
        };

        meta.InstalledVersion = modVersion;
        meta.LastModInstalledAt = DateTimeOffset.Now;

        var metaDir = Path.Combine(instancePath, "manager-data");
        if (!Directory.Exists(metaDir))
        {
            Directory.CreateDirectory(metaDir);
        }

        var metaPath = Path.Combine(metaDir, "instance.json");
        var metaJson = JsonSerializer.Serialize(meta, JsonOpts);
        await File.WriteAllTextAsync(metaPath, metaJson);
    }

    public async Task<InstanceMetadata?> ReadInstanceMetadataAsync(string instancePath)
    {
        var metaPath = Path.Combine(instancePath, "manager-data", "instance.json");
        if (!File.Exists(metaPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(metaPath);
            return JsonSerializer.Deserialize<InstanceMetadata>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteInstanceAsync(InstanceItem instance)
    {
        if (instance == null || string.IsNullOrWhiteSpace(instance.Path))
            return false;

        var path = Path.GetFullPath(instance.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 1. Zabezpieczenie: czy instancja jest zarejestrowana
        var registered = await LoadRegistryPathsAsync();
        var matching = registered.FirstOrDefault(p => string.Equals(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), path, StringComparison.OrdinalIgnoreCase));

        if (matching == null)
        {
            throw new InvalidOperationException("Ta ścieżka nie znajduje się na liście zarządzanych instancji.");
        }

        // 2. Krytyczne zabezpieczenie: absolutny zakaz usuwania katalogu głównego dysku lub katalogu aplikacji lub Windows
        var root = Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(root, path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Niedozwolona operacja: próba usunięcia głównego katalogu dysku!");
        }

        var appBase = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(appBase, path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Niedozwolona operacja: nie można usunąć folderu aplikacji!");
        }

        // 3. Sprawdzenie obecności instance.json
        var metaFile = Path.Combine(path, "manager-data", "instance.json");
        if (!File.Exists(metaFile))
        {
            throw new InvalidOperationException("W folderze brakuje pliku manager-data/instance.json potwierdzającego, że to bezpieczna instancja.");
        }

        // 4. Usuwanie fizyczne z dysku
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        // 5. Usunięcie z rejestru
        registered.RemoveAll(p => string.Equals(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), path, StringComparison.OrdinalIgnoreCase));
        await SaveRegistryPathsAsync(registered);

        return true;
    }

    public bool IsValidInstance(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        var exe = Path.Combine(path, "Among Us.exe");
        var meta = Path.Combine(path, "manager-data", "instance.json");

        return File.Exists(exe) && File.Exists(meta);
    }
}
