using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using AmongUsModManager.Models;

namespace AmongUsModManager.Helpers;

public record PackageParseResult(bool IsSuccess, string? ErrorMessage, ModPackageInfo? Package = null);

public static partial class ModPackageParser
{
    // Flexible version regex matching vX.Y.Z anywhere in string (e.g. "v1.7.3")
    [GeneratedRegex(@"v(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"(?<arch>x86|x64)", RegexOptions.IgnoreCase)]
    private static partial Regex ArchPattern();

    [GeneratedRegex(@"(?<variant>steam-itch|steam|itch)", RegexOptions.IgnoreCase)]
    private static partial Regex VariantPattern();

    public static PackageParseResult TryParse(string zipFilePath)
    {
        if (string.IsNullOrWhiteSpace(zipFilePath))
        {
            return new PackageParseResult(false, "Nie wybrano pliku ZIP.");
        }

        if (!File.Exists(zipFilePath))
        {
            return new PackageParseResult(false, $"Wskazany plik nie istnieje:\n{zipFilePath}");
        }

        var fileName = Path.GetFileName(zipFilePath);
        var fi = new FileInfo(zipFilePath);

        List<string> normalizedEntries;
        try
        {
            using var zip = ZipFile.OpenRead(zipFilePath);
            normalizedEntries = zip.Entries
                .Select(e => e.FullName.Replace('\\', '/').Trim('/'))
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            if (normalizedEntries.Count == 0)
            {
                return new PackageParseResult(false, $"Plik ZIP '{fileName}' jest pusty.");
            }
        }
        catch (InvalidDataException)
        {
            return new PackageParseResult(false, $"Plik '{fileName}' jest uszkodzony lub nie jest poprawnym archiwum ZIP.");
        }
        catch (Exception ex)
        {
            return new PackageParseResult(false, $"Błąd odczytu archiwum ZIP '{fileName}':\n{ex.Message}");
        }

        // 1. Determine payload root directory inside ZIP that contains characteristic TouMira files
        var (payloadRoot, signatureCount) = DetectPayloadRoot(normalizedEntries);

        if (signatureCount < 2)
        {
            return new PackageParseResult(
                false,
                $"Wskazany plik ZIP '{fileName}' nie zawiera wystarczającej liczby charakterystycznych elementów moda TouMira.\n\n" +
                $"Wykryto sygnatur: {signatureCount} (wymagane minimum: 2 z listy: BepInEx, dotnet, winhttp.dll, doorstop_config.ini, .doorstop_version).\n\n" +
                $"Upewnij się, że pobrałeś właściwą paczkę moda.");
        }

        // 2. Extract version info (prioritize filename, then payload root folder name)
        var versionMatch = VersionPattern().Match(fileName);
        if (!versionMatch.Success && !string.IsNullOrEmpty(payloadRoot))
        {
            versionMatch = VersionPattern().Match(payloadRoot);
        }

        if (!versionMatch.Success)
        {
            // Scan entry paths as fallback
            foreach (var entry in normalizedEntries)
            {
                var m = VersionPattern().Match(entry);
                if (m.Success)
                {
                    versionMatch = m;
                    break;
                }
            }
        }

        if (!versionMatch.Success)
        {
            return new PackageParseResult(
                false,
                $"Nie udało się jednoznacznie ustalić numeru wersji moda (brak oznaczenia 'vX.Y.Z' w nazwie pliku ani w strukturze archiwum).\n\n" +
                $"Plik: {fileName}");
        }

        int major = int.Parse(versionMatch.Groups["major"].Value);
        int minor = int.Parse(versionMatch.Groups["minor"].Value);
        int patch = int.Parse(versionMatch.Groups["patch"].Value);
        var version = new Version(major, minor, patch);
        var rawVersion = $"v{major}.{minor}.{patch}";

        // Architecture detection (default x86)
        var archMatch = ArchPattern().Match(fileName);
        if (!archMatch.Success && !string.IsNullOrEmpty(payloadRoot))
        {
            archMatch = ArchPattern().Match(payloadRoot);
        }
        var arch = archMatch.Success ? archMatch.Groups["arch"].Value.ToLowerInvariant() : "x86";

        // Variant detection (default steam-itch)
        var variantMatch = VariantPattern().Match(fileName);
        if (!variantMatch.Success && !string.IsNullOrEmpty(payloadRoot))
        {
            variantMatch = VariantPattern().Match(payloadRoot);
        }
        var variant = variantMatch.Success ? variantMatch.Groups["variant"].Value.ToLowerInvariant() : "steam-itch";

        var package = new ModPackageInfo
        {
            ModName = "TouMira",
            Version = version,
            RawVersionString = rawVersion,
            Architecture = arch,
            Variant = variant,
            ZipFilePath = zipFilePath,
            FileName = fileName,
            PayloadRootPrefix = string.IsNullOrEmpty(payloadRoot) ? "" : payloadRoot + "/",
            FileSizeBytes = fi.Length,
            DetectedSignaturesCount = signatureCount
        };

        return new PackageParseResult(true, null, package);
    }

    private static (string PayloadRoot, int SignatureCount) DetectPayloadRoot(List<string> entries)
    {
        // Candidate roots: root "" and all top-level / sub directory prefixes
        var candidateRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "" };

        foreach (var entry in entries)
        {
            var parts = entry.Split('/');
            if (parts.Length > 1)
            {
                // Add 1st level folder
                candidateRoots.Add(parts[0]);
            }
            if (parts.Length > 2)
            {
                // Add 2nd level folder
                candidateRoots.Add($"{parts[0]}/{parts[1]}");
            }
        }

        string bestRoot = "";
        int maxSignatures = -1;

        foreach (var candidate in candidateRoots)
        {
            int score = CalculateSignatureScore(entries, candidate);
            if (score > maxSignatures)
            {
                maxSignatures = score;
                bestRoot = candidate;
            }
        }

        return (bestRoot, maxSignatures);
    }

    private static int CalculateSignatureScore(List<string> entries, string rootPrefix)
    {
        string prefix = string.IsNullOrEmpty(rootPrefix) ? "" : rootPrefix + "/";

        bool hasBepInEx = entries.Any(e => e.Equals(prefix + "BepInEx", StringComparison.OrdinalIgnoreCase) ||
                                           e.StartsWith(prefix + "BepInEx/", StringComparison.OrdinalIgnoreCase));

        bool hasDotnet = entries.Any(e => e.Equals(prefix + "dotnet", StringComparison.OrdinalIgnoreCase) ||
                                          e.StartsWith(prefix + "dotnet/", StringComparison.OrdinalIgnoreCase));

        bool hasWinHttp = entries.Any(e => e.Equals(prefix + "winhttp.dll", StringComparison.OrdinalIgnoreCase));

        bool hasDoorstopIni = entries.Any(e => e.Equals(prefix + "doorstop_config.ini", StringComparison.OrdinalIgnoreCase));

        bool hasDoorstopVer = entries.Any(e => e.Equals(prefix + ".doorstop_version", StringComparison.OrdinalIgnoreCase));

        bool hasOtherTouMira = entries.Any(e => e.Contains("TouMira", StringComparison.OrdinalIgnoreCase));

        int count = 0;
        if (hasBepInEx) count++;
        if (hasDotnet) count++;
        if (hasWinHttp) count++;
        if (hasDoorstopIni) count++;
        if (hasDoorstopVer) count++;
        if (hasOtherTouMira) count++;

        return count;
    }
}
