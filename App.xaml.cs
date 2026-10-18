using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AmongUsModManager.Services;
using AmongUsModManager.Services.Interfaces;
using AmongUsModManager.ViewModels;

namespace AmongUsModManager;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<IGamePathValidator, GamePathValidator>();
        services.AddSingleton<ISteamLocatorService, SteamLocatorService>();
        services.AddSingleton<IProcessChecker, ProcessChecker>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IGitHubReleaseService, GitHubReleaseService>();
        services.AddSingleton<IModInstallerService, ModInstallerService>();
        services.AddSingleton<IInstanceManagerService, InstanceManagerService>();
        services.AddSingleton<IModCleanupService, ModCleanupService>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }
}
