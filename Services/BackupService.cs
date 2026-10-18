using System.IO;
using System.IO.Compression;
using AmongUsModManager.Models;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public class BackupService : IBackupService
{
    private readonly IConfigService _configService;

    public BackupService(IConfigService configService)
    {
        _configService = configService;
    }

    public async Task<BackupEntry> CreateBackupAsync(string gamePath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var backupsDir = _configService.GetBackupsDirectory();
        if (!Directory.Exists(backupsDir))
        {
            Directory.CreateDirectory(backupsDir);
        }

        var timestamp = DateTimeOffset.Now;
        var fileName = $"backup_AmongUs_{timestamp:yyyyMMdd_HHmmss}.zip";
        var zipFilePath = Path.Combine(backupsDir, fileName);

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(gamePath, "*", SearchOption.AllDirectories);
            var totalFiles = files.Length;

            using (var zipStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                for (int i = 0; i < totalFiles; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var file = files[i];
                    var relativePath = Path.GetRelativePath(gamePath, file);

                    try
                    {
                        archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
                    }
                    catch (IOException)
                    {
                        // Ignore locked transient files if any
                    }

                    progress?.Report((double)(i + 1) / totalFiles * 100.0);
                }
            }
        }, ct);

        var fileInfo = new FileInfo(zipFilePath);
        var entry = new BackupEntry
        {
            FileName = fileName,
            FullPath = zipFilePath,
            CreatedAt = timestamp,
            SizeBytes = fileInfo.Length
        };

        _configService.State.Backups.Add(entry);
        RotateBackups(_configService.Config.MaxBackups);
        _configService.SaveState();

        return entry;
    }

    public IReadOnlyList<BackupEntry> GetExistingBackups()
    {
        var backupsDir = _configService.GetBackupsDirectory();
        if (!Directory.Exists(backupsDir))
        {
            return Array.Empty<BackupEntry>();
        }

        var zipFiles = Directory.GetFiles(backupsDir, "*.zip");
        var list = new List<BackupEntry>();

        foreach (var file in zipFiles)
        {
            var fi = new FileInfo(file);
            list.Add(new BackupEntry
            {
                FileName = fi.Name,
                FullPath = fi.FullName,
                CreatedAt = fi.CreationTimeUtc,
                SizeBytes = fi.Length
            });
        }

        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public void RotateBackups(int maxBackups = 2)
    {
        if (maxBackups < 1) maxBackups = 1;

        var existing = GetExistingBackups();
        if (existing.Count > maxBackups)
        {
            var toDelete = existing.Skip(maxBackups).ToList();
            foreach (var item in toDelete)
            {
                try
                {
                    if (File.Exists(item.FullPath))
                    {
                        File.Delete(item.FullPath);
                    }
                }
                catch
                {
                    // Ignore deletion error
                }
            }
        }

        // Synchronize state list
        _configService.State.Backups = GetExistingBackups().ToList();
        _configService.SaveState();
    }
}
