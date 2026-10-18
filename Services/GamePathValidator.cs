using System.IO;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public class GamePathValidator : IGamePathValidator
{
    private static readonly string[] DangerousPaths =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
    };

    public ValidationResult Validate(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return new ValidationResult(false, "Ścieżka do folderu gry nie została podana.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(directoryPath.Trim());
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Nieprawidłowy format ścieżki: {ex.Message}");
        }

        if (!Directory.Exists(fullPath))
        {
            return new ValidationResult(false, "Wskazany folder nie istnieje na dysku.");
        }

        // Check if root of drive (e.g., C:\, D:\)
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath.TrimEnd('\\', '/'), root?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult(false, "Bezpieczeństwo: Nie wolno wskazywać głównego katalogu dysku!");
        }

        // Check for protected / system root directories
        foreach (var dangerousPath in DangerousPaths)
        {
            if (!string.IsNullOrEmpty(dangerousPath) &&
                string.Equals(fullPath.TrimEnd('\\', '/'), dangerousPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(false, $"Bezpieczeństwo: Nie wolno wskazywać folderu systemowego ({Path.GetFileName(dangerousPath)})!");
            }
        }

        // Check for Among Us.exe
        var exePath = Path.Combine(fullPath, "Among Us.exe");
        if (!File.Exists(exePath))
        {
            return new ValidationResult(false, "Nie znaleziono pliku 'Among Us.exe' w wybranym folderze.");
        }

        // Check for Unity structure signature (Among Us_Data or UnityPlayer.dll)
        var dataFolder = Path.Combine(fullPath, "Among Us_Data");
        var unityDll = Path.Combine(fullPath, "UnityPlayer.dll");
        var gameAssembly = Path.Combine(fullPath, "GameAssembly.dll");

        if (!Directory.Exists(dataFolder) && !File.Exists(unityDll) && !File.Exists(gameAssembly))
        {
            return new ValidationResult(false, "Brak sygnatury gry Unity (brak 'Among Us_Data', 'UnityPlayer.dll' ani 'GameAssembly.dll').");
        }

        return new ValidationResult(true, "Poprawna instalacja Among Us", exePath);
    }

    public string DetectGameVersion(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return "Nieznana";
        }

        try
        {
            // 1. Sprawdzamy oficjalny ciąg wersji Innersloth w pliku globalgamemanagers
            var ggmPath = Path.Combine(directoryPath, "Among Us_Data", "globalgamemanagers");
            if (File.Exists(ggmPath))
            {
                using var stream = File.OpenRead(ggmPath);
                using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 4096);
                var content = reader.ReadToEnd();

                // Szukamy wersji Among Us w formacie np. 2026.8.18 lub 2024.x.x
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"202[0-9]\.[0-9]+(\.[0-9]+)?");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    // Pomijamy numery wersji samego silnika Unity (np. 2022.3.44)
                    if (!m.Value.StartsWith("2022.3", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"v{m.Value}";
                    }
                }
            }

            // 2. Fallback: odczyt wersji pliku wykonywalnego
            var exePath = Path.Combine(directoryPath, "Among Us.exe");
            if (File.Exists(exePath))
            {
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(vi.ProductVersion))
                {
                    return $"Build {vi.ProductVersion.Split(' ')[0]}";
                }
            }
        }
        catch
        {
            // W razie braku uprawnień lub błędu odczytu nie przerywamy działania
        }

        return "Nierozpoznana";
    }
}
