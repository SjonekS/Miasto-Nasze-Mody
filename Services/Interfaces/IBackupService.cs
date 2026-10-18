using AmongUsModManager.Models;

namespace AmongUsModManager.Services.Interfaces;

public interface IBackupService
{
    Task<BackupEntry> CreateBackupAsync(string gamePath, IProgress<double>? progress = null, CancellationToken ct = default);
    IReadOnlyList<BackupEntry> GetExistingBackups();
    void RotateBackups(int maxBackups = 2);
}
