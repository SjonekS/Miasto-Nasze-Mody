namespace AmongUsModManager.Services.Interfaces;

public interface ISteamLocatorService
{
    string? FindAmongUsSteamPath();
    IReadOnlyList<string> GetAllSteamLibraryFolders();
}
