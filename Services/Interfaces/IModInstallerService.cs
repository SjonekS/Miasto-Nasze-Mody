using AmongUsModManager.Models;

namespace AmongUsModManager.Services.Interfaces;

public record OperationProgress(double Percentage, string Status);

public enum VersionComparison
{
    FirstInstall,
    SameVersion,
    OlderVersion,
    NewerVersion
}

public interface IModInstallerService
{
    VersionComparison CompareWithInstalled(ModPackageInfo package, string? customInstalledVersionString = null);
    string? ResolveGamePath();
    ValidationResult ValidateCurrentGamePath();
    Task<bool> InstallPackageAsync(ModPackageInfo package, string targetGamePath, IProgress<OperationProgress>? progress = null, CancellationToken ct = default);
}
