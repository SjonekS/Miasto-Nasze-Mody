namespace AmongUsModManager.Services.Interfaces;

public interface IProcessChecker
{
    bool IsGameRunning();
    bool TryKillGame();
}
