namespace AmongUsModManager.Services.Interfaces;

public record ValidationResult(bool IsValid, string Message, string? ExecutablePath = null);

public interface IGamePathValidator
{
    ValidationResult Validate(string? directoryPath);
    string DetectGameVersion(string? directoryPath);
}
