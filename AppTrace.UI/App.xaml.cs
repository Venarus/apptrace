using System.Windows;
using AppTrace.Core.Data;
using AppTrace.Core.Services;
using AppTrace.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AppTrace.UI;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        _serviceProvider = serviceCollection.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services & Data
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAppDetectionService, AppDetectionService>();
        services.AddSingleton<IUninstallerService, UninstallerService>();
        services.AddSingleton<ILeftoverScannerService, LeftoverScannerService>();
        services.AddSingleton<IQuarantineService, QuarantineService>();
        services.AddSingleton<ICleanupService, CleanupService>();
        services.AddSingleton<IStartupManagerService, StartupManagerService>();
        services.AddSingleton<IStorageAnalysisService, StorageAnalysisService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views / Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
