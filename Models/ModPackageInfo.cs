namespace AmongUsModManager.Models;

public class InstalledModInfo
{
    public string ModName { get; set; } = "TouMira";
    public string VersionString { get; set; } = "";
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }
    public string Architecture { get; set; } = "x86";
    public string Variant { get; set; } = "steam-itch";
    public DateTimeOffset InstalledAt { get; set; }

    public Version AsVersion() => new(Major, Minor, Patch);
}

public class ModPackageInfo
{
    public string ModName { get; set; } = "TouMira";
    public Version Version { get; set; } = new(0, 0, 0);
    public string RawVersionString { get; set; } = "";
    public string Architecture { get; set; } = "x86";
    public string Variant { get; set; } = "steam-itch";
    public string ZipFilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string PayloadRootPrefix { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public int DetectedSignaturesCount { get; set; }
}
