namespace AmongUsModManager.Models;

public class DriveItemInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string? ModifiedTime { get; set; }
    public long? Size { get; set; }
    public string RelativePath { get; set; } = "";
    public bool IsFolder => MimeType == "application/vnd.google-apps.folder";
}

public class ModVersionInfo
{
    /// <summary>
    /// Name of the version folder, e.g. "TouMira v1.7.3".
    /// </summary>
    public string VersionName { get; set; } = "";

    /// <summary>
    /// Google Drive Folder ID of the version folder.
    /// </summary>
    public string VersionFolderId { get; set; } = "";

    /// <summary>
    /// Modified time string (ISO 8601) as returned by Google Drive.
    /// </summary>
    public string ModifiedTime { get; set; } = "";

    /// <summary>
    /// Parsed DateTimeOffset representation of ModifiedTime.
    /// </summary>
    public DateTimeOffset? ParsedModifiedTime { get; set; }

    /// <summary>
    /// Total files count inside the version.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Total size in bytes of all files in the version.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Flattened list of files to download (with relative paths).
    /// </summary>
    public List<DriveItemInfo> Files { get; set; } = new();
}
