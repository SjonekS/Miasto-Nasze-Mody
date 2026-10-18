using AmongUsModManager.Models;

namespace AmongUsModManager.Services.Interfaces;

public interface IInstanceManagerService
{
    /// <summary>
    /// Pobiera listę wszystkich zarejestrowanych i poprawnych instancji.
    /// </summary>
    Task<List<InstanceItem>> GetAllInstancesAsync();

    /// <summary>
    /// Waliduje proponowaną ścieżkę nowej instancji pod kątem bezpieczeństwa i poprawności.
    /// </summary>
    ValidationResult ValidateNewInstanceLocation(string parentFolder, string instanceName, string sourceGamePath);

    /// <summary>
    /// Tworzy nową niezależną kopię Among Us wraz z plikiem metadanych instance.json.
    /// </summary>
    Task<InstanceItem> CreateInstanceAsync(
        string instanceName, 
        string targetDirectory, 
        string sourceGamePath, 
        IProgress<OperationProgress>? progress = null, 
        CancellationToken ct = default);

    /// <summary>
    /// Aktualizuje metadane instancji po pomyślnej instalacji modów (zapis do instance.json).
    /// </summary>
    Task UpdateInstanceModVersionAsync(string instancePath, string modVersion);

    /// <summary>
    /// Odczytuje metadane instancji z jej folderu (manager-data/instance.json).
    /// </summary>
    Task<InstanceMetadata?> ReadInstanceMetadataAsync(string instancePath);

    /// <summary>
    /// Bezpiecznie usuwa instancję z dysku oraz z listy instancji po weryfikacji ścieżki.
    /// </summary>
    Task<bool> DeleteInstanceAsync(InstanceItem instance);

    /// <summary>
    /// Sprawdza, czy dana ścieżka jest prawidłową instancją stworzoną przez menedżera.
    /// </summary>
    bool IsValidInstance(string path);
}
