namespace AmongUsModManager.Models;

public class GitHubReleaseInfo
{
    public string TagName { get; set; } = "";
    public Version Version { get; set; } = new(0, 0, 0);
    public string VersionString { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
