using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public partial class SteamLocatorService : ISteamLocatorService
{
    private const string AmongUsFolderName = "Among Us";
    private const string AmongUsAppId = "945360";
    private readonly IGamePathValidator _validator;

    [GeneratedRegex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

    public SteamLocatorService(IGamePathValidator validator)
    {
        _validator = validator;
    }

    public string? FindAmongUsSteamPath()
    {
        var libraries = GetAllSteamLibraryFolders();
        foreach (var library in libraries)
        {
            var candidate = Path.Combine(library, "steamapps", "common", AmongUsFolderName);
            var validation = _validator.Validate(candidate);
            if (validation.IsValid)
            {
                return candidate;
            }
        }

        return null;
    }

    public IReadOnlyList<string> GetAllSteamLibraryFolders()
    {
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var steamInstall = GetSteamInstallPath();
        if (!string.IsNullOrEmpty(steamInstall) && Directory.Exists(steamInstall))
        {
            list.Add(steamInstall);

            var vdfPath = Path.Combine(steamInstall, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    var content = File.ReadAllText(vdfPath);
                    var matches = LibraryPathRegex().Matches(content);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var rawPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            if (Directory.Exists(rawPath))
                            {
                                list.Add(rawPath);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore VDF read error and proceed with base steam path
                }
            }
        }

        // Fallback common drive letters
        var commonDrives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName);

        foreach (var drive in commonDrives)
        {
            var candidate1 = Path.Combine(drive, "SteamLibrary");
            if (Directory.Exists(candidate1)) list.Add(candidate1);

            var candidate2 = Path.Combine(drive, "Program Files (x86)", "Steam");
            if (Directory.Exists(candidate2)) list.Add(candidate2);

            var candidate3 = Path.Combine(drive, "Steam");
            if (Directory.Exists(candidate3)) list.Add(candidate3);
        }

        return list.ToList();
    }

    private static string? GetSteamInstallPath()
    {
        try
        {
            // Registry check HKCU
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                var val = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(val))
                {
                    return val.Replace('/', '\\');
                }
            }

            // Registry check HKLM 64-bit
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
            {
                var val = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(val))
                {
                    return val;
                }
            }

            // Registry check HKLM 32-bit
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                var val = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(val))
                {
                    return val;
                }
            }
        }
        catch
        {
            // Registry access denied or unavailable
        }

        return null;
    }
}
