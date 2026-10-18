namespace AmongUsModManager.Models;

public class BackupEntry
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public long SizeBytes { get; set; }
}
