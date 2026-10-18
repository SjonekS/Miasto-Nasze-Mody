using CommunityToolkit.Mvvm.ComponentModel;

namespace AmongUsModManager.Models;

/// <summary>
/// Informacja o instancji Among Us prezentowana w UI i rejestrowana w systemie.
/// Dziedziczy z ObservableObject dla płynnego odświeżania właściwości w WPF.
/// </summary>
public partial class InstanceItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _path = "";

    [ObservableProperty]
    private string _gameVersion = "Nieznana";

    [ObservableProperty]
    private string? _installedVersion;

    [ObservableProperty]
    private DateTimeOffset _createdAt = DateTimeOffset.Now;

    [ObservableProperty]
    private DateTimeOffset? _lastModInstalledAt;

    [ObservableProperty]
    private bool _isValid = true;

    [ObservableProperty]
    private string _statusText = "Gotowa";

    public string DisplayVersion => string.IsNullOrWhiteSpace(InstalledVersion) ? "Brak modów" : InstalledVersion;
    public string DisplayGameVersion => string.IsNullOrWhiteSpace(GameVersion) ? "Wersja gry nieznana" : $"Gra: {GameVersion}";
}
