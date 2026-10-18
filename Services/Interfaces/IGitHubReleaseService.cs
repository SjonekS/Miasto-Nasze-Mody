using AmongUsModManager.Models;

namespace AmongUsModManager.Services.Interfaces;

public interface IGitHubReleaseService
{
    Task<GitHubReleaseInfo?> GetLatestTouMiraReleaseAsync(Action<string>? logCallback = null, CancellationToken ct = default);
    Task<string> DownloadAssetAsync(GitHubReleaseInfo release, string destinationDirectory, IProgress<OperationProgress>? progress = null, Action<string>? logCallback = null, CancellationToken ct = default);
}
