namespace AmongUsModManager.Models;

/// <summary>
/// Metadane instancji gry Among Us zapisywane wewnątrz jej folderu w `manager-data/instance.json`.
/// </summary>
public class InstanceMetadata
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? InstalledVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastModInstalledAt { get; set; }
}
