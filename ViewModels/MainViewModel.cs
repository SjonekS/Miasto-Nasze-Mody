using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AmongUsModManager.Helpers;
using AmongUsModManager.Models;
using AmongUsModManager.Services;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IModInstallerService _modInstaller;
    private readonly IGitHubReleaseService _gitHubService;
    private readonly ISteamLocatorService _steamLocator;
    private readonly IGamePathValidator _pathValidator;
    private readonly IBackupService _backupService;
    private readonly IProcessChecker _processChecker;
    private readonly IInstanceManagerService _instanceManager;
    private readonly IModCleanupService _modCleanup;

    [ObservableProperty]
    private string _gamePath = "";

    [ObservableProperty]
    private bool _isGamePathValid;

    [ObservableProperty]
    private string _gamePathStatus = "Wyszukiwanie folderu gry...";

    [ObservableProperty]
    private string _gameVersionDisplay = "Wersja gry: wykrywanie...";

    [ObservableProperty]
    private string _installedVersion = "Brak";

    [ObservableProperty]
    private string _installedDetails = "Nie zainstalowano jeszcze modów przez managera";

    [ObservableProperty]
    private string _latestRemoteVersion = "-";

    [ObservableProperty]
    private string _mainStatusMessage = "Gotowy do działania.";

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private GitHubReleaseInfo? _latestRelease;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private string _progressTitle = "";

    [ObservableProperty]
    private string _progressDetail = "";

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private bool _autoBackup = true;

    [ObservableProperty]
    private int _backupsCount;

    [ObservableProperty]
    private bool _isSettingsVisible;

    [ObservableProperty]
    private bool _isTroubleshootVisible;

    [ObservableProperty]
    private bool _isLogsVisible;

    [ObservableProperty]
    private ObservableCollection<string> _logs = new();

    [ObservableProperty]
    private bool _isInstancesVisible;

    [ObservableProperty]
    private ObservableCollection<InstanceItem> _instances = new();

    [ObservableProperty]
    private InstanceItem? _selectedInstance;

    [ObservableProperty]
    private bool _isOperatingOnInstance;

    [ObservableProperty]
    private string _activeTargetTitle = "Główna instalacja gry (Steam)";

    [ObservableProperty]
    private string _newInstanceName = "TouMira";

    [ObservableProperty]
    private string _newInstanceParentFolder = "";

    [ObservableProperty]
    private bool _isCreateInstanceDialogVisible;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Główna gra Steam, 1 = Instancje / Kapsuły

    [ObservableProperty]
    private string _selectedThemeId = "SteelBlue";

    [ObservableProperty]
    private ThemeDefinition? _selectedTheme;

    [ObservableProperty]
    private bool _isManualDeletionEnabled;

    [ObservableProperty]
    private int _currentGuideStep = 1;

    [ObservableProperty]
    private string _guideTitle = "Krok 1: Główna gra Steam";

    [ObservableProperty]
    private string _guideDescription = "Pobieraj i aktualizuj modyfikację TouMira jednym kliknięciem dla swojej standardowej instalacji Steam.";

    [ObservableProperty]
    private string _guideStepDisplay = "Krok 1 z 4";

    [RelayCommand]
    private void NextGuideStep()
    {
        CurrentGuideStep = CurrentGuideStep >= 4 ? 1 : CurrentGuideStep + 1;
        UpdateGuideDisplay();
    }

    [RelayCommand]
    private void PreviousGuideStep()
    {
        CurrentGuideStep = CurrentGuideStep <= 1 ? 4 : CurrentGuideStep - 1;
        UpdateGuideDisplay();
    }

    private void UpdateGuideDisplay()
    {
        GuideStepDisplay = $"Krok {CurrentGuideStep} z 4";
        switch (CurrentGuideStep)
        {
            case 1:
                GuideTitle = "1. Główna gra Steam";
                GuideDescription = "Pobieraj i aktualizuj modyfikację TouMira jednym kliknięciem. Graj zawsze na najnowszej wersji ze znajomymi.";
                break;
            case 2:
                GuideTitle = "2. Kapsuły Czasu (Instancje)";
                GuideDescription = "Twórz niezależne kopie Among Us z zamrożonymi modami. Żadna aktualizacja Steam nie popsuje Twojej gry!";
                break;
            case 3:
                GuideTitle = "3. Pomoc & Czyszczenie";
                GuideDescription = "W razie problemów użyj 'Szybkiego czyszczenia plugins' lub suwaka pełnego resetu z zachowaniem ustawień modów.";
                break;
            case 4:
                GuideTitle = "4. Personalizacja & Kopie";
                GuideDescription = "W Ustawieniach dopasuj motyw kolorystyczny. Przed każdą instalacją manager automatycznie chroni Twoje pliki kopią ZIP.";
                break;
        }
    }

    partial void OnSelectedThemeChanged(ThemeDefinition? value)
    {
        if (value == null) return;
        SelectedThemeId = value.Id;
        ThemeManager.ApplyTheme(value.Id);
        _configService.Config.SelectedTheme = value.Id;
        _configService.SaveConfig();
        AddLog($"Zmieniono motyw kolorystyczny na: {value.Name}");
    }

    public List<ThemeDefinition> AvailableThemes => ThemeManager.AvailableThemes;

    public string ActionInstallButtonText => IsOperatingOnInstance
        ? $"Zainstaluj w instancji: {SelectedInstance?.Name}"
        : "Pobierz i zainstaluj (Główna gra Steam)";

    public MainViewModel(
        IConfigService configService,
        IModInstallerService modInstaller,
        IGitHubReleaseService gitHubService,
        ISteamLocatorService steamLocator,
        IGamePathValidator pathValidator,
        IBackupService backupService,
        IProcessChecker processChecker,
        IInstanceManagerService instanceManager,
        IModCleanupService modCleanup)
    {
        _configService = configService;
        _modInstaller = modInstaller;
        _gitHubService = gitHubService;
        _steamLocator = steamLocator;
        _pathValidator = pathValidator;
        _backupService = backupService;
        _processChecker = processChecker;
        _instanceManager = instanceManager;
        _modCleanup = modCleanup;

        LoadInitialData();
    }

    private void LoadInitialData()
    {
        AutoBackup = _configService.Config.AutoBackupBeforeUpdate;
        SelectedThemeId = string.IsNullOrWhiteSpace(_configService.Config.SelectedTheme) ? "SteelBlue" : _configService.Config.SelectedTheme;
        SelectedTheme = AvailableThemes.FirstOrDefault(t => t.Id == SelectedThemeId) ?? AvailableThemes[0];
        ThemeManager.ApplyTheme(SelectedTheme.Id);

        UpdateInstalledVersionDisplay();
        RefreshGamePath();
        RefreshBackupsCount();
        _ = RefreshInstancesAsync();
        UpdateGuideDisplay();
        AddLog("Aplikacja Among Us Mod Manager v2.0 uruchomiona (System Kapsuł Czasu aktywny).");

        // Sprawdź aktualizacje automatycznie w tle przy starcie
        _ = CheckForUpdatesAsync(isInitialCheck: true);
    }

    [RelayCommand]
    private void SetTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int tab))
        {
            // Zmiana zakładki automatycznie zamyka wszelkie nakładki
            IsSettingsVisible = false;
            IsTroubleshootVisible = false;
            IsLogsVisible = false;

            SelectedTabIndex = tab;
            if (tab == 0)
            {
                CurrentGuideStep = 1;
                UpdateGuideDisplay();
                SwitchToSteamContext();
            }
            else if (tab == 1)
            {
                CurrentGuideStep = 2;
                UpdateGuideDisplay();
                _ = RefreshInstancesAsync();
            }
        }
    }

    [RelayCommand]
    private void ChangeTheme(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId)) return;
        var theme = AvailableThemes.FirstOrDefault(t => t.Id == themeId);
        if (theme != null)
        {
            SelectedTheme = theme;
        }
    }

    private void UpdateInstalledVersionDisplay()
    {
        if (IsOperatingOnInstance && SelectedInstance != null)
        {
            InstalledVersion = SelectedInstance.InstalledVersion ?? "Brak";
            InstalledDetails = string.IsNullOrWhiteSpace(SelectedInstance.InstalledVersion)
                ? $"Instancja: {SelectedInstance.Name} • Czysta instalacja gry"
                : $"Instancja: {SelectedInstance.Name} • Wersja moda: {SelectedInstance.InstalledVersion}";
            return;
        }

        var mod = _configService.State.InstalledMod;
        if (mod != null)
        {
            InstalledVersion = mod.VersionString;
            InstalledDetails = $"{mod.ModName} ({mod.Architecture}, {mod.Variant}) • Zainstalowano: {mod.InstalledAt:yyyy-MM-dd HH:mm}";
        }
        else
        {
            InstalledVersion = "Brak";
            InstalledDetails = "Mody nie zostały jeszcze zainstalowane.";
        }
    }

    public void AddLog(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Logs.Insert(0, entry);
            if (Logs.Count > 150) Logs.RemoveAt(Logs.Count - 1);
        });
    }

    private void RefreshBackupsCount()
    {
        var backups = _backupService.GetExistingBackups();
        BackupsCount = backups.Count;
    }

    private void RefreshGamePath()
    {
        var resolved = _modInstaller.ResolveGamePath();
        GamePath = resolved ?? "";

        if (string.IsNullOrWhiteSpace(GamePath))
        {
            IsGamePathValid = false;
            GamePathStatus = "Wskaż folder instalacji gry Among Us (z plikiem Among Us.exe).";
            GameVersionDisplay = "Wersja gry: brak";
            return;
        }

        var val = _pathValidator.Validate(GamePath);
        IsGamePathValid = val.IsValid;
        GamePathStatus = val.IsValid ? "Prawidłowa instalacja gry" : val.Message;
        GameVersionDisplay = val.IsValid ? $"Wersja Among Us: {_pathValidator.DetectGameVersion(GamePath)}" : "Wersja gry: nieznana";
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync(bool isInitialCheck = false)
    {
        if (IsBusy) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressTitle = "Sprawdzanie dostępności aktualizacji...";
        ProgressDetail = "Łączenie z serwerem wydań GitHub...";
        MainStatusMessage = "Sprawdzanie aktualizacji...";
        AddLog("Sprawdzanie najnowszego wydania TouMira na GitHub...");

        try
        {
            var release = await _gitHubService.GetLatestTouMiraReleaseAsync(log => AddLog(log));
            if (release != null)
            {
                LatestRelease = release;
                LatestRemoteVersion = release.VersionString;

                string? currentVerStr = IsOperatingOnInstance && SelectedInstance != null
                    ? SelectedInstance.InstalledVersion
                    : _configService.State.InstalledMod?.VersionString;

                if (string.IsNullOrWhiteSpace(currentVerStr))
                {
                    IsUpdateAvailable = true;
                    MainStatusMessage = $"Dostępna jest wersja {release.VersionString}.";
                    AddLog($"Dostępna wersja {release.VersionString} (brak zainstalowanego moda).");
                }
                else
                {
                    Version.TryParse(currentVerStr.TrimStart('v', 'V'), out var installedVer);
                    installedVer ??= new Version(0, 0, 0);

                    if (release.Version > installedVer)
                    {
                        IsUpdateAvailable = true;
                        MainStatusMessage = $"Dostępna jest nowa wersja: TouMira {release.VersionString}!";
                        AddLog($"Wykryto nowszą wersję: {currentVerStr} -> {release.VersionString}");
                    }
                    else if (release.Version < installedVer)
                    {
                        IsUpdateAvailable = false;
                        MainStatusMessage = $"Zainstalowana wersja ({currentVerStr}) jest nowsza niż na serwerze.";
                        AddLog($"Lokalna wersja jest nowsza niż wydanie na serwerze.");
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        MainStatusMessage = "Masz najnowszą wersję modów.";
                        AddLog($"Modyfikacje są aktualne ({currentVerStr}).");
                    }
                }
            }
            else
            {
                LatestRemoteVersion = "Brak informacji";
                MainStatusMessage = "Nie udało się sprawdzić aktualizacji. Sprawdź połączenie z internetem.";
                AddLog("Nie udało się połączyć z serwerem wydań GitHub.");
            }
        }
        catch (Exception ex)
        {
            LatestRemoteVersion = "Brak połączenia";
            MainStatusMessage = "Nie udało się sprawdzić aktualizacji.";
            AddLog($"Błąd połączenia: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallAsync()
    {
        if (IsBusy) return;

        // 1. Sprawdzenie czy gra nie jest uruchomiona
        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show(
                "Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed aktualizacją modów, aby uniknąć problemów z blokadą plików.",
                "Gra jest uruchomiona",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // 2. Wybór i walidacja folderu docelowego (instancja lub główna gra)
        var gamePath = IsOperatingOnInstance && SelectedInstance != null
            ? SelectedInstance.Path
            : _modInstaller.ResolveGamePath();

        var pathValidation = _pathValidator.Validate(gamePath);
        if (!pathValidation.IsValid || string.IsNullOrEmpty(gamePath))
        {
            MessageBox.Show(
                $"Nieprawidłowy folder docelowy:\n{pathValidation.Message}\n\nWskaż poprawny folder gry w aplikacji.",
                "Błąd folderu docelowego",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // 3. Jeśli brak informacji o release, spróbuj pobrać
        if (LatestRelease == null)
        {
            await CheckForUpdatesAsync();
            if (LatestRelease == null)
            {
                MessageBox.Show(
                    "Nie udało się połączyć z serwerem wydań GitHub.\n\nMożesz użyć opcji 'Wybierz ZIP ręcznie', jeśli posiadasz pobrany plik moda.",
                    "Brak połączenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }

        var release = LatestRelease;
        var installedVerText = IsOperatingOnInstance && SelectedInstance != null
            ? (SelectedInstance.InstalledVersion ?? "Brak")
            : (_configService.State.InstalledMod?.VersionString ?? "Brak");

        // 4. Proste i czytelne podsumowanie dla użytkownika
        var confirmResult = MessageBox.Show(
            $"Czy chcesz zainstalować aktualizację modyfikacji?\n\n" +
            $"• Wersja do zainstalowania: {release.VersionString}\n" +
            $"• Obecna wersja: {installedVerText}\n" +
            $"• Rozmiar pakietu: ~{release.SizeBytes / (1024 * 1024):0} MB\n\n" +
            $"Docelowy folder gry:\n{gamePath}",
            "Instalacja modów TouMira",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes)
        {
            AddLog("Anulowano instalację na prośbę użytkownika.");
            return;
        }

        IsBusy = true;
        IsProgressIndeterminate = false;
        ProgressPercentage = 0;
        ProgressTitle = $"Pobieranie TouMira {release.VersionString}...";
        ProgressDetail = "Inicjalizacja połączenia...";
        MainStatusMessage = "Pobieranie aktualizacji...";
        AddLog($"Rozpoczęto pobieranie {release.AssetName}...");

        string? downloadedZipPath = null;
        try
        {
            var tempDir = Path.Combine(_configService.GetAppDataDirectory(), ".temp");
            var downloadProg = new Progress<OperationProgress>(p =>
            {
                ProgressPercentage = p.Percentage * 0.45; // 0 - 45% na pobieranie
                ProgressDetail = p.Status;
            });

            downloadedZipPath = await _gitHubService.DownloadAssetAsync(release, tempDir, downloadProg, log => AddLog(log));
            AddLog("Pobieranie zakończone pomyślnie.");

            // Przejście do bezpiecznego pipeline'u instalacji
            await ExecuteInstallPipelineCoreAsync(downloadedZipPath, gamePath, 45.0);
        }
        catch (Exception ex)
        {
            MainStatusMessage = "Nie udało się pobrać aktualizacji.";
            AddLog($"BŁĄD: {ex.Message}");

            string friendlyMessage = GetFriendlyErrorMessage(ex);
            MessageBox.Show(
                $"{friendlyMessage}\n\nLokalna instalacja gry NIE została zmieniona.",
                "Błąd pobierania",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            if (!string.IsNullOrEmpty(downloadedZipPath) && File.Exists(downloadedZipPath))
            {
                try { File.Delete(downloadedZipPath); } catch { }
            }

            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
        }
    }

    [RelayCommand]
    private async Task SelectManualZipAsync()
    {
        if (IsBusy) return;

        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show(
                "Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed instalacją modów.",
                "Gra jest uruchomiona",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var gamePath = IsOperatingOnInstance && SelectedInstance != null
            ? SelectedInstance.Path
            : _modInstaller.ResolveGamePath();

        var pathValidation = _pathValidator.Validate(gamePath);
        if (!pathValidation.IsValid || string.IsNullOrEmpty(gamePath))
        {
            MessageBox.Show(
                $"Nieprawidłowy folder docelowy:\n{pathValidation.Message}\n\nWskaż poprawny folder gry w aplikacji.",
                "Błąd folderu gry",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var openDialog = new OpenFileDialog
        {
            Title = "Wskaż pobrany plik ZIP z modem TouMira",
            Filter = "Paczka moda TouMira (*.zip)|*.zip|Wszystkie pliki (*.*)|*.*",
            Multiselect = false
        };

        if (openDialog.ShowDialog() != true)
        {
            return;
        }

        var selectedZipPath = openDialog.FileName;

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressPercentage = 0;
        ProgressTitle = "Sprawdzanie wybranego pliku ZIP...";
        ProgressDetail = Path.GetFileName(selectedZipPath);
        MainStatusMessage = "Sprawdzanie pliku...";

        try
        {
            await ExecuteInstallPipelineCoreAsync(selectedZipPath, gamePath, 0.0);
        }
        catch (Exception ex)
        {
            MainStatusMessage = "Nie udało się zainstalować modyfikacji.";
            AddLog($"BŁĄD: {ex.Message}");

            string friendlyMessage = GetFriendlyErrorMessage(ex);
            MessageBox.Show(
                $"{friendlyMessage}\n\nLokalna instalacja gry NIE została zmieniona.",
                "Błąd instalacji",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
        }
    }

    private async Task ExecuteInstallPipelineCoreAsync(string zipFilePath, string gamePath, double startPercentageOffset)
    {
        // 1. Walidacja zawartości i sygnatur
        ProgressTitle = "Sprawdzanie zawartości paczki...";
        IsProgressIndeterminate = true;
        AddLog($"Analiza struktury pliku {Path.GetFileName(zipFilePath)}...");

        var parseResult = ModPackageParser.TryParse(zipFilePath);
        if (!parseResult.IsSuccess || parseResult.Package == null)
        {
            throw new InvalidOperationException(parseResult.ErrorMessage ?? "Wybrany plik nie wygląda na prawidłową paczkę TouMira.");
        }

        var package = parseResult.Package;
        AddLog($"Wykryto poprawną paczkę: {package.ModName} {package.RawVersionString}");

        // 2. Porównanie wersji w przypadku ręcznego wyboru
        if (startPercentageOffset == 0.0)
        {
            string? currentVerStr = IsOperatingOnInstance && SelectedInstance != null
                ? SelectedInstance.InstalledVersion
                : _configService.State.InstalledMod?.VersionString;

            var comparison = _modInstaller.CompareWithInstalled(package, currentVerStr);

            if (comparison == VersionComparison.SameVersion)
            {
                var sameConfirm = MessageBox.Show(
                    $"Wersja {package.RawVersionString} jest już aktualnie zainstalowana.\n\nCzy chcesz zainstalować ją ponownie?",
                    "Ta sama wersja",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (sameConfirm != MessageBoxResult.Yes)
                {
                    AddLog("Anulowano ponowną instalację tej samej wersji.");
                    MainStatusMessage = "Gotowy do działania.";
                    return;
                }
            }
            else if (comparison == VersionComparison.OlderVersion && !string.IsNullOrWhiteSpace(currentVerStr))
            {
                var downgradeConfirm = MessageBox.Show(
                    $"Wybrana wersja ({package.RawVersionString}) jest starsza niż obecnie zainstalowana ({currentVerStr}).\n\nCzy na pewno chcesz zainstalować starszą wersję?",
                    "Instalacja starszej wersji",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (downgradeConfirm != MessageBoxResult.Yes)
                {
                    AddLog("Anulowano instalację starszej wersji.");
                    MainStatusMessage = "Gotowy do działania.";
                    return;
                }
            }
        }

        // 3. Wykonanie instalacji (backup -> rozpakowanie -> verify -> stan)
        IsProgressIndeterminate = false;
        var prog = new Progress<OperationProgress>(p =>
        {
            double scale = (100.0 - startPercentageOffset) / 100.0;
            ProgressPercentage = startPercentageOffset + (p.Percentage * scale);
            ProgressTitle = p.Status;
            ProgressDetail = $"{ProgressPercentage:0}%";
            AddLog(p.Status);
        });

        MainStatusMessage = "Instalowanie aktualizacji...";
        var success = await _modInstaller.InstallPackageAsync(package, gamePath, prog);

        if (success)
        {
            if (IsOperatingOnInstance && SelectedInstance != null)
            {
                SelectedInstance.InstalledVersion = package.RawVersionString;
                SelectedInstance.LastModInstalledAt = DateTimeOffset.Now;
                SelectedInstance.StatusText = $"Zainstalowany mod: {package.RawVersionString}";
            }

            UpdateInstalledVersionDisplay();
            RefreshBackupsCount();
            MainStatusMessage = $"Gotowe. TouMira {package.RawVersionString} jest zainstalowana.";
            AddLog($"Instalacja TouMira {package.RawVersionString} zakończona sukcesem.");

            _ = CheckForUpdatesAsync();

            MessageBox.Show(
                $"Modyfikacja została pomyślnie zainstalowana i jest gotowa do uruchomienia!\n\nZainstalowana wersja: TouMira {package.RawVersionString}",
                "Aktualizacja zakończona sukcesem",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static string GetFriendlyErrorMessage(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return "Brak uprawnień do zapisu w folderze gry.\nUruchom program jako Administrator lub sprawdź uprawnienia do folderu Among Us.";
        }

        if (ex is HttpRequestException || ex is IOException)
        {
            return "Wystąpił problem z pobraniem pliku z internetu.\nSprawdź połączenie z siecią i spróbuj ponownie.";
        }

        if (ex is TaskCanceledException)
        {
            return "Przekroczono limit czasu połączenia podczas pobierania aktualizacji.";
        }

        return ex.Message;
    }

    [RelayCommand]
    private void AutoDetectSteam()
    {
        var steam = _steamLocator.FindAmongUsSteamPath();
        if (!string.IsNullOrEmpty(steam))
        {
            _configService.Config.CustomGamePath = null;
            _configService.SaveConfig();
            RefreshGamePath();
            AddLog($"Wykryto instalację Steam: {steam}");
            MessageBox.Show($"Wykryto grę Among Us w bibliotece Steam:\n{steam}", "Wykryto grę", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Nie odnaleziono gry Among Us w bibliotekach Steam.\nMożesz wskazać folder ręcznie.", "Brak wykrycia", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void BrowseGamePath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder instalacji gry Among Us (zawierający Among Us.exe)"
        };

        if (dialog.ShowDialog() == true)
        {
            var selected = dialog.FolderName;
            var val = _pathValidator.Validate(selected);
            if (!val.IsValid)
            {
                MessageBox.Show(val.Message, "Nieprawidłowy folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _configService.Config.CustomGamePath = selected;
            _configService.SaveConfig();
            RefreshGamePath();
            AddLog($"Ustawiono ręczną ścieżkę gry: {selected}");
        }
    }

    [RelayCommand]
    private void OpenGameFolder()
    {
        if (Directory.Exists(GamePath))
        {
            Process.Start(new ProcessStartInfo { FileName = GamePath, UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void OpenBackupsFolder()
    {
        var dir = _configService.GetBackupsDirectory();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    [RelayCommand]
    private void LaunchGame()
    {
        if (!IsGamePathValid) return;

        var exePath = Path.Combine(GamePath, "Among Us.exe");
        if (File.Exists(exePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = GamePath,
                UseShellExecute = true
            });
            AddLog("Uruchomiono grę Among Us.");
        }
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsVisible = !IsSettingsVisible;
        if (IsSettingsVisible)
        {
            IsTroubleshootVisible = false;
            IsLogsVisible = false;
            CurrentGuideStep = 4;
            UpdateGuideDisplay();
        }
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsVisible = false;
    }

    [RelayCommand]
    private void ToggleTroubleshoot()
    {
        IsTroubleshootVisible = !IsTroubleshootVisible;
        if (IsTroubleshootVisible)
        {
            IsSettingsVisible = false;
            IsLogsVisible = false;
            CurrentGuideStep = 3;
            UpdateGuideDisplay();
        }
    }

    [RelayCommand]
    private void CloseTroubleshoot()
    {
        IsTroubleshootVisible = false;
    }

    [RelayCommand]
    private void ToggleLogs()
    {
        IsLogsVisible = !IsLogsVisible;
        if (IsLogsVisible)
        {
            IsSettingsVisible = false;
            IsTroubleshootVisible = false;
        }
    }

    [RelayCommand]
    private void CloseLogs()
    {
        IsLogsVisible = false;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _configService.Config.AutoBackupBeforeUpdate = AutoBackup;
        _configService.SaveConfig();

        AddLog("Zapisano ustawienia.");
        IsSettingsVisible = false;
    }

    [RelayCommand]
    private void CleanTroubleshootMods()
    {
        if (!IsGamePathValid)
        {
            MessageBox.Show("Folder gry nie został prawidłowo skonfigurowany.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            "Manager otworzy folder gry w Eksploratorze Windows.\n\n" +
            "Aby ręcznie zresetować mody, możesz usunąć:\n" +
            "• folder BepInEx\n• folder dotnet\n• pliki winhttp.dll oraz doorstop_config.ini\n\n" +
            "Czy chcesz otworzyć folder gry?",
            "Bezpieczne czyszczenie modów",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            OpenGameFolder();
            AddLog("Otwarto folder gry do procedury czyszczenia.");
        }
    }

    private bool IsSafeGameDirectory(string path, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            error = "Folder gry nie istnieje lub ścieżka jest pusta.";
            return false;
        }

        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            error = "Niedozwolona operacja: wskazana ścieżka to główny katalog dysku!";
            return false;
        }

        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrEmpty(winDir) && fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
        {
            error = "Niedozwolona operacja: ścieżka znajduje się w katalogu systemowym Windows!";
            return false;
        }

        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrEmpty(progFiles) && string.Equals(fullPath, progFiles, StringComparison.OrdinalIgnoreCase))
        {
            error = "Niedozwolona operacja: nie można operować bezpośrednio na katalogu Program Files!";
            return false;
        }

        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrEmpty(progFilesX86) && string.Equals(fullPath, progFilesX86, StringComparison.OrdinalIgnoreCase))
        {
            error = "Niedozwolona operacja: nie można operować bezpośrednio na katalogu Program Files (x86)!";
            return false;
        }

        var appDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (fullPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
        {
            error = "Niedozwolona operacja: folder docelowy to katalog aplikacji Managera!";
            return false;
        }

        var exePath = Path.Combine(fullPath, "Among Us.exe");
        if (!File.Exists(exePath))
        {
            error = "Folder nie zawiera pliku 'Among Us.exe'. Odmowa wykonania operacji ze względów bezpieczeństwa.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task HardClearModsAsync()
    {
        if (!IsManualDeletionEnabled)
        {
            MessageBox.Show("Włącz suwak zezwolenia na usuwanie plików w oknie Pomoc, aby odblokować tę funkcję.", "Funkcja zablokowana", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show("Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed wykonaniem czyszczenia.", "Gra jest uruchomiona", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targetPath = GamePath;
        if (!IsSafeGameDirectory(targetPath, out var err))
        {
            MessageBox.Show(err, "Błąd bezpieczeństwa", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            "Czy na pewno chcesz usunąć pliki modyfikacji z folderu gry?\n\n" +
            "Zostaną usunięte:\n" +
            "• Folder BepInEx (Twoje ustawienia w BepInEx/config zostaną ZACHOWANE)\n" +
            "• Folder dotnet\n" +
            "• Pliki winhttp.dll oraz doorstop_config.ini\n\n" +
            "Czyste pliki gry Among Us pozostaną nienaruszone.\n\n" +
            "Czy chcesz kontynuować?",
            "Potwierdzenie czyszczenia modów",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var backupAsk = MessageBox.Show(
            "Czy chcesz przed usunięciem wykonać kopię zapasową folderu gry?\n\n" +
            "(Kopia nie jest wymagana, ale możesz ją utworzyć w razie potrzeby)",
            "Kopia zapasowa",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (backupAsk == MessageBoxResult.Cancel) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressTitle = "Czyszczenie plików modyfikacji...";
        ProgressDetail = "Trwa usuwanie modów...";
        MainStatusMessage = "Czyszczenie modów...";

        try
        {
            if (backupAsk == MessageBoxResult.Yes)
            {
                ProgressTitle = "Tworzenie kopii zapasowej...";
                AddLog("Tworzenie kopii zapasowej przed czyszczeniem modów...");
                await _backupService.CreateBackupAsync(targetPath);
                RefreshBackupsCount();
                AddLog("Kopia zapasowa została utworzona.");
            }

            ProgressTitle = "Usuwanie plików modyfikacji...";

            // 1. Zabezpieczenie BepInEx/config
            var bepInExDir = Path.Combine(targetPath, "BepInEx");
            var configDir = Path.Combine(bepInExDir, "config");
            string? tempConfigBackup = null;

            if (Directory.Exists(configDir))
            {
                tempConfigBackup = Path.Combine(Path.GetTempPath(), $"AmongUs_config_backup_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempConfigBackup);
                foreach (var f in Directory.GetFiles(configDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(configDir, f);
                    var dest = Path.Combine(tempConfigBackup, rel);
                    var pDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(pDir)) Directory.CreateDirectory(pDir);
                    File.Copy(f, dest, true);
                }
            }

            // 2. Usunięcie BepInEx
            if (Directory.Exists(bepInExDir))
            {
                Directory.Delete(bepInExDir, true);
            }

            // 3. Przywrócenie config jeśli istniał
            if (tempConfigBackup != null && Directory.Exists(tempConfigBackup))
            {
                Directory.CreateDirectory(configDir);
                foreach (var f in Directory.GetFiles(tempConfigBackup, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(tempConfigBackup, f);
                    var dest = Path.Combine(configDir, rel);
                    var pDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(pDir)) Directory.CreateDirectory(pDir);
                    File.Copy(f, dest, true);
                }
                try { Directory.Delete(tempConfigBackup, true); } catch { }
            }

            // 4. Usunięcie dotnet, winhttp.dll, doorstop_config.ini
            var dotnetDir = Path.Combine(targetPath, "dotnet");
            if (Directory.Exists(dotnetDir))
            {
                Directory.Delete(dotnetDir, true);
            }

            var winhttpFile = Path.Combine(targetPath, "winhttp.dll");
            if (File.Exists(winhttpFile))
            {
                File.Delete(winhttpFile);
            }

            var doorstopFile = Path.Combine(targetPath, "doorstop_config.ini");
            if (File.Exists(doorstopFile))
            {
                File.Delete(doorstopFile);
            }

            // 5. Reset stanu w managerze
            _configService.State.InstalledMod = null;
            _configService.SaveState();

            UpdateInstalledVersionDisplay();
            AddLog("Pomyślnie wykonano całkowite wyczyszczenie plików modów (config zachowany).");

            MessageBox.Show(
                "Modyfikacje zostały całkowicie usunięte z folderu gry.\n\n" +
                "Folder konfiguracji (BepInEx/config) został zachowany.\n" +
                "Gra Among Us posiada teraz czyste pliki bazowe bez modów.",
                "Czyszczenie zakończone",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLog($"Błąd czyszczenia modów: {ex.Message}");
            MessageBox.Show($"Wystąpił błąd podczas usuwania plików modów:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
            MainStatusMessage = "Gotowy do działania.";
        }
    }

    [RelayCommand]
    private async Task HardClearGameFilesAsync()
    {
        if (!IsManualDeletionEnabled)
        {
            MessageBox.Show("Włącz suwak zezwolenia na usuwanie plików w oknie Pomoc, aby odblokować tę funkcję.", "Funkcja zablokowana", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show("Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed wykonaniem czyszczenia.", "Gra jest uruchomiona", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targetPath = GamePath;
        if (!IsSafeGameDirectory(targetPath, out var err))
        {
            MessageBox.Show(err, "Błąd bezpieczeństwa", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var backupAsk = MessageBox.Show(
            "Czy chcesz przed usunięciem wykonać kopię zapasową folderu gry Among Us?\n\n" +
            "(Kopia nie jest wymagana, ale możesz ją utworzyć w razie potrzeby)",
            "Kopia zapasowa",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (backupAsk == MessageBoxResult.Cancel) return;

        var confirm = MessageBox.Show(
            $"⚠️ UWAGA: Za chwilę zostaną bezpowrotnie usunięte WSZYSTKIE pliki gry Among Us z folderu:\n\n{targetPath}\n\n" +
            "Operacja ta przygotuje grę do czystej reinstalacji na platformie Steam.\n\n" +
            "Czy na pewno chcesz usunąć wszystkie pliki gry?",
            "Potwierdzenie usunięcia wszystkich plików gry",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressTitle = "Usuwanie wszystkich plików gry...";
        ProgressDetail = "Trwa usuwanie zawartości folderu...";
        MainStatusMessage = "Usuwanie plików gry Among Us...";

        try
        {
            if (backupAsk == MessageBoxResult.Yes)
            {
                ProgressTitle = "Tworzenie kopii zapasowej...";
                AddLog("Tworzenie kopii zapasowej przed pełnym usunięciem gry...");
                await _backupService.CreateBackupAsync(targetPath);
                RefreshBackupsCount();
                AddLog("Kopia zapasowa została utworzona.");
            }

            ProgressTitle = "Usuwanie plików gry Among Us...";

            var dirInfo = new DirectoryInfo(targetPath);
            foreach (var file in dirInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (var subDir in dirInfo.GetDirectories())
            {
                if (subDir.Name.Equals("backups", StringComparison.OrdinalIgnoreCase))
                    continue;

                subDir.Delete(true);
            }

            _configService.State.InstalledMod = null;
            _configService.SaveState();

            RefreshGamePath();
            UpdateInstalledVersionDisplay();
            AddLog("Usunięto wszystkie pliki Among Us z folderu gry w celu ponownej instalacji.");

            MessageBox.Show(
                "Wszystkie pliki gry Among Us zostały pomyślnie usunięte z folderu Steam.\n\n" +
                "CO NALEŻY TERAZ ZROBIĆ (Krok po kroku):\n\n" +
                "1. Otwórz aplikację Steam na komputerze.\n" +
                "2. W Bibliotece gier kliknij prawym przyciskiem myszy na 'Among Us' -> 'Zarządzaj' -> 'Odinstaluj' (lub 'Właściwości' -> 'Zainstalowane pliki' -> 'Sprawdź spójność plików gry').\n" +
                "3. Kliknij 'Zainstaluj' w Steamie, aby pobrać w 100% świeżą, oryginalną wersję gry od twórców (Innersloth).\n" +
                "4. Po zakończeniu pobierania przez Steam, wróć do tego Managera i kliknij 'Pobierz i zainstaluj na Steam'.",
                "Pliki gry usunięte - Instrukcja dalszych kroków",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLog($"Błąd usuwania plików gry: {ex.Message}");
            MessageBox.Show($"Wystąpił błąd podczas usuwania plików gry:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
            MainStatusMessage = "Gotowy do działania.";
        }
    }

    [RelayCommand]
    private async Task QuickCleanPluginsAsync()
    {
        if (!IsGamePathValid)
        {
            MessageBox.Show("Folder gry nie został prawidłowo skonfigurowany.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show("Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed czyszczeniem folderu pluginów.", "Gra jest uruchomiona", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targetPath = GamePath;
        if (!IsSafeGameDirectory(targetPath, out var err))
        {
            MessageBox.Show(err, "Błąd bezpieczeństwa", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show(
            "Czy na pewno chcesz wyczyścić folder BepInEx/plugins?\n\n" +
            "Zostaną usunięte wyłącznie pliki wewnątrz folderu plugins.\n" +
            "Folder config, core, gra oraz kopie zapasowe pozostaną nienaruszone.\n\n" +
            "Możesz następnie ponownie zainstalować mody przez Managera.",
            "Szybkie czyszczenie folderu plugins",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressTitle = "Czyszczenie folderu plugins...";
        ProgressDetail = "Trwa usuwanie plików z BepInEx/plugins...";
        MainStatusMessage = "Czyszczenie plugins...";

        try
        {
            var success = await _modCleanup.QuickCleanPluginsFolderAsync(targetPath, msg => AddLog(msg));

            if (success)
            {
                AddLog("Pomyślnie wyczyszczono folder BepInEx/plugins.");
                MessageBox.Show(
                    "Folder BepInEx/plugins został wyczyszczony.\n\n" +
                    "Możesz teraz zainstalować nową wersję modów przez Managera.",
                    "Wyczyszczono plugins",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AddLog($"Błąd czyszczenia plugins: {ex.Message}");
            MessageBox.Show($"Wystąpił błąd podczas czyszczenia plugins:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
            MainStatusMessage = "Gotowy do działania.";
        }
    }

    [RelayCommand]
    private void ToggleInstances()
    {
        IsInstancesVisible = !IsInstancesVisible;
        if (IsInstancesVisible)
        {
            IsSettingsVisible = false;
            IsTroubleshootVisible = false;
            IsLogsVisible = false;
            _ = RefreshInstancesAsync();
        }
    }

    [RelayCommand]
    public async Task RefreshInstancesAsync()
    {
        try
        {
            var list = await _instanceManager.GetAllInstancesAsync();
            Instances.Clear();
            foreach (var inst in list)
            {
                Instances.Add(inst);
            }
        }
        catch (Exception ex)
        {
            AddLog($"Błąd odświeżania listy instancji: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SwitchToSteamContext()
    {
        IsOperatingOnInstance = false;
        SelectedInstance = null;
        ActiveTargetTitle = "Główna instalacja gry (Steam)";
        OnPropertyChanged(nameof(ActionInstallButtonText));
        RefreshGamePath();
        UpdateInstalledVersionDisplay();
        AddLog("Przełączono kontekst na główną instalację gry.");
        _ = CheckForUpdatesAsync();
    }

    [RelayCommand]
    private void SelectInstanceContext(InstanceItem? instance)
    {
        if (instance == null) return;

        SelectedInstance = instance;
        IsOperatingOnInstance = true;
        ActiveTargetTitle = $"Instancja: {instance.Name}";
        OnPropertyChanged(nameof(ActionInstallButtonText));
        GamePath = instance.Path;
        var val = _pathValidator.Validate(instance.Path);
        IsGamePathValid = val.IsValid;
        GamePathStatus = val.IsValid ? $"Aktywna niezależna instancja: {instance.Name}" : val.Message;
        GameVersionDisplay = val.IsValid ? $"Wersja Among Us: {_pathValidator.DetectGameVersion(instance.Path)}" : "Wersja gry: nieznana";

        InstalledVersion = instance.InstalledVersion ?? "Brak";
        InstalledDetails = string.IsNullOrWhiteSpace(instance.InstalledVersion)
            ? $"Czysta instalacja • Utworzono: {instance.CreatedAt:yyyy-MM-dd HH:mm}"
            : $"Zainstalowany mod: {instance.InstalledVersion} • Utworzono: {instance.CreatedAt:yyyy-MM-dd HH:mm}";

        IsInstancesVisible = false;
        AddLog($"Przełączono kontekst pracy na instancję: {instance.Name} ({instance.Path})");
        _ = CheckForUpdatesAsync();
    }

    [RelayCommand]
    private void ShowCreateInstanceDialog()
    {
        NewInstanceName = $"TouMira_{DateTime.Now:yyyyMMdd}";
        if (string.IsNullOrWhiteSpace(NewInstanceParentFolder))
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            NewInstanceParentFolder = Path.Combine(docs, "Among Us Instances");
        }

        IsCreateInstanceDialogVisible = true;
    }

    [RelayCommand]
    private void CloseCreateInstanceDialog()
    {
        IsCreateInstanceDialogVisible = false;
    }

    [RelayCommand]
    private void BrowseNewInstanceParentFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wskaż folder bazowy, w którym zostanie utworzona instancja (np. D:\\Among Us Instances)"
        };

        if (dialog.ShowDialog() == true)
        {
            NewInstanceParentFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task ConfirmCreateInstanceAsync()
    {
        if (IsBusy) return;

        var sourcePath = _modInstaller.ResolveGamePath();
        if (string.IsNullOrEmpty(sourcePath) || !_pathValidator.Validate(sourcePath).IsValid)
        {
            MessageBox.Show("Nie wykryto poprawnej głównej instalacji Among Us jako źródła do skopiowania gry.", "Brak źródła", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var val = _instanceManager.ValidateNewInstanceLocation(NewInstanceParentFolder, NewInstanceName, sourcePath);
        if (!val.IsValid)
        {
            MessageBox.Show(val.Message, "Nieprawidłowe dane instancji", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var targetPath = Path.Combine(NewInstanceParentFolder, NewInstanceName);

        var confirm = MessageBox.Show(
            $"Czy chcesz utworzyć nową niezależną instancję Among Us?\n\n" +
            $"• Nazwa: {NewInstanceName}\n" +
            $"• Ścieżka docelowa:\n{targetPath}\n\n" +
            $"• Zostaną skopiowane czyste pliki gry z:\n{sourcePath}\n\n" +
            $"Główna instalacja gry pozostanie całkowicie nienaruszona.",
            "Potwierdzenie utworzenia instancji",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsCreateInstanceDialogVisible = false;
        IsBusy = true;
        IsProgressIndeterminate = false;
        ProgressPercentage = 0;
        ProgressTitle = $"Tworzenie instancji: {NewInstanceName}...";
        ProgressDetail = "Kopiowanie plików gry...";
        MainStatusMessage = "Kopiowanie instalacji Among Us...";
        AddLog($"Rozpoczęto tworzenie niezależnej instancji '{NewInstanceName}' w {targetPath}...");

        try
        {
            var prog = new Progress<OperationProgress>(p =>
            {
                ProgressPercentage = p.Percentage;
                ProgressTitle = p.Status;
                ProgressDetail = $"{p.Percentage:0}%";
            });

            var createdItem = await _instanceManager.CreateInstanceAsync(NewInstanceName, targetPath, sourcePath, prog);
            await RefreshInstancesAsync();
            SelectInstanceContext(createdItem);

            MessageBox.Show(
                $"Instancja '{NewInstanceName}' została pomyślnie utworzona!\n\nMożesz teraz niezależnie zainstalować w niej modyfikację TouMira bez wpływu na główną grę.",
                "Instancja utworzona",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLog($"BŁĄD tworzenia instancji: {ex.Message}");
            MessageBox.Show($"Nie udało się utworzyć instancji:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
            MainStatusMessage = "Gotowy do działania.";
        }
    }

    [RelayCommand]
    private async Task DeleteInstanceAsync(InstanceItem? item)
    {
        if (item == null || IsBusy) return;

        var confirm = MessageBox.Show(
            $"Czy na pewno chcesz bezpowrotnie usunąć instancję '{item.Name}'?\n\n" +
            $"Ścieżka do usunięcia:\n{item.Path}\n\n" +
            $"Zostaną usunięte wyłącznie pliki w powyższym folderze instancji. Główna gra ani inne instancje NIE zostaną naruszone.",
            "Usuwanie instancji",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _instanceManager.DeleteInstanceAsync(item);
            AddLog($"Usunięto instancję '{item.Name}' ({item.Path}).");

            if (IsOperatingOnInstance && SelectedInstance?.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase) == true)
            {
                SwitchToSteamContext();
            }

            await RefreshInstancesAsync();

            MessageBox.Show($"Instancja '{item.Name}' została usunięta.", "Usunięto", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLog($"Błąd podczas usuwania instancji: {ex.Message}");
            MessageBox.Show($"Błąd usuwania instancji:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenInstanceFolder(InstanceItem? item)
    {
        if (item != null && Directory.Exists(item.Path))
        {
            Process.Start(new ProcessStartInfo { FileName = item.Path, UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void LaunchInstance(InstanceItem? item)
    {
        if (item == null) return;
        var exePath = Path.Combine(item.Path, "Among Us.exe");
        if (!File.Exists(exePath))
        {
            MessageBox.Show($"Nie znaleziono pliku Among Us.exe w kapsule:\n{item.Path}", "Błąd uruchamiania", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = item.Path,
                UseShellExecute = true
            });
            AddLog($"Uruchomiono grę Among Us z kapsuły: {item.Name}");
        }
        catch (Exception ex)
        {
            AddLog($"Błąd uruchamiania gry z kapsuły: {ex.Message}");
            MessageBox.Show($"Nie udało się uruchomić gry z kapsuły:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task UpdateInstanceModAsync(InstanceItem? item)
    {
        if (item == null || IsBusy) return;

        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show(
                "Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed aktualizacją modów.",
                "Gra jest uruchomiona",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (LatestRelease == null)
        {
            await CheckForUpdatesAsync();
            if (LatestRelease == null)
            {
                MessageBox.Show(
                    "Nie udało się połączyć z serwerem wydań GitHub.\n\nMożesz użyć opcji 'Wskaż ZIP', jeśli posiadasz pobrany plik moda.",
                    "Brak połączenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }

        var release = LatestRelease;
        var confirmResult = MessageBox.Show(
            $"Czy chcesz zainstalować wersję {release.VersionString} do kapsuły '{item.Name}'?\n\n" +
            $"• Docelowa kapsuła: {item.Name}\n" +
            $"• Ścieżka kapsuły:\n{item.Path}\n\n" +
            $"Główna instalacja Steam oraz pozostałe kapsuły pozostaną nienaruszone.",
            "Instalacja modów w kapsule",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes) return;

        IsBusy = true;
        IsProgressIndeterminate = false;
        ProgressPercentage = 0;
        ProgressTitle = $"Pobieranie TouMira dla {item.Name}...";
        ProgressDetail = "Inicjalizacja...";
        MainStatusMessage = $"Aktualizacja kapsuły {item.Name}...";

        string? downloadedZipPath = null;
        try
        {
            var tempDir = Path.Combine(_configService.GetAppDataDirectory(), ".temp");
            var downloadProg = new Progress<OperationProgress>(p =>
            {
                ProgressPercentage = p.Percentage * 0.45;
                ProgressDetail = p.Status;
            });

            downloadedZipPath = await _gitHubService.DownloadAssetAsync(release, tempDir, downloadProg, log => AddLog(log));

            await ExecuteInstallPipelineCoreAsync(downloadedZipPath, item.Path, 45.0);

            await _instanceManager.UpdateInstanceModVersionAsync(item.Path, release.VersionString);
            item.InstalledVersion = release.VersionString;
            item.StatusText = $"Zainstalowany mod: {release.VersionString}";

            AddLog($"Pomyślnie zaktualizowano mody w kapsule {item.Name} do wersji {release.VersionString}.");
            MessageBox.Show(
                $"Kapsuła '{item.Name}' została pomyślnie zaktualizowana do wersji TouMira {release.VersionString}!",
                "Aktualizacja kapsuły zakończona",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLog($"BŁĄD aktualizacji kapsuły: {ex.Message}");
            MessageBox.Show($"Nie udało się zaktualizować kapsuły:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (!string.IsNullOrEmpty(downloadedZipPath) && File.Exists(downloadedZipPath))
            {
                try { File.Delete(downloadedZipPath); } catch { }
            }
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
            MainStatusMessage = "Gotowy do działania.";
        }
    }

    [RelayCommand]
    private async Task InstallZipToInstanceAsync(InstanceItem? item)
    {
        if (item == null || IsBusy) return;

        if (_processChecker.IsGameRunning())
        {
            MessageBox.Show("Gra Among Us jest aktualnie włączona!\n\nZamknij grę przed instalacją modów.", "Gra jest uruchomiona", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var openDialog = new OpenFileDialog
        {
            Title = $"Wskaż pobrany plik ZIP z modem TouMira dla kapsuły {item.Name}",
            Filter = "Paczka moda TouMira (*.zip)|*.zip|Wszystkie pliki (*.*)|*.*",
            Multiselect = false
        };

        if (openDialog.ShowDialog() != true) return;

        var selectedZipPath = openDialog.FileName;
        IsBusy = true;
        IsProgressIndeterminate = true;
        ProgressTitle = $"Instalacja ZIP w kapsule {item.Name}...";
        ProgressDetail = Path.GetFileName(selectedZipPath);

        try
        {
            await ExecuteInstallPipelineCoreAsync(selectedZipPath, item.Path, 0.0);

            var parseResult = ModPackageParser.TryParse(selectedZipPath);
            var verStr = parseResult.Package?.RawVersionString ?? "Custom";
            await _instanceManager.UpdateInstanceModVersionAsync(item.Path, verStr);
            item.InstalledVersion = verStr;
            item.StatusText = $"Zainstalowany mod: {verStr}";

            AddLog($"Pomyślnie zainstalowano mod z ZIP w kapsule {item.Name}.");
            MessageBox.Show(
                $"Modyfikacja z pliku ZIP została zainstalowana w kapsule '{item.Name}'!",
                "Sukces",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLog($"BŁĄD instalacji ZIP w kapsule: {ex.Message}");
            MessageBox.Show($"Nie udało się zainstalować pliku ZIP:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            ProgressTitle = "";
            ProgressDetail = "";
        }
    }
}
