using System.Diagnostics;
using AmongUsModManager.Services.Interfaces;

namespace AmongUsModManager.Services;

public class ProcessChecker : IProcessChecker
{
    private const string ProcessName = "Among Us";

    public bool IsGameRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(ProcessName);
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public bool TryKillGame()
    {
        try
        {
            var processes = Process.GetProcessesByName(ProcessName);
            foreach (var p in processes)
            {
                p.Kill();
                p.WaitForExit(3000);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
