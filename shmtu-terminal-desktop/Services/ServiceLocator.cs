using System.Reactive;
using Splat;
using shmtu.terminal.desktop.Services.BillClassification;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.Services.Sync;
using shmtu.terminal.desktop.ViewModels;
using shmtu.terminal.desktop.ViewModels.Component;
using shmtu.terminal.desktop.ViewModels.Component.Bill;
using shmtu.terminal.desktop.ViewModels.Component.CountDown;
using shmtu.terminal.desktop.ViewModels.MainWindowTab;
using shmtu.terminal.desktop.ViewModels.Program;
using shmtu.terminal.desktop.ViewModels.Startup;
using shmtu.terminal.desktop.ViewModels.User;

namespace shmtu.terminal.desktop.Services;

/// <summary>
/// Service locator — registers all services, database managers, and ViewModels
/// Uses Splat (included with ReactiveUI) for dependency injection
/// </summary>
public static class ServiceLocator
{
    private static bool _isInitialized;

    /// <summary>
    /// Register all services and ViewModels
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        var container = Locator.CurrentMutable;

        // Register services
        RegisterServices(container);

        // Register ViewModels
        RegisterViewModels(container);
    }

    private static void RegisterServices(IMutableDependencyResolver container)
    {
        // Navigation service as singleton
        container.RegisterLazySingleton(() => NavigationService.Instance);

        // Bill sync service — new instance each time (stateful with events)
        container.Register(() => new BillSyncService());

        // Bill classifier — new instance each time (loads rules from disk)
        container.Register(() => new BillClassifier());
    }

    private static void RegisterViewModels(IMutableDependencyResolver container)
    {
        // Main window ViewModel
        container.RegisterLazySingleton(() => new MainWindowViewModel());

        // Tab ViewModels
        container.Register(() => new HomeTabViewModel());
        container.Register(() => new BillTabViewModel());
        container.Register(() => new FeaturesTabViewModel());

        // Program ViewModels
        container.Register(() => new SettingsViewModel());
        container.Register(() => new AboutViewModel());
        container.Register(() => new DataTransferViewModel());
        container.Register(() => new CaptchaTestViewModel());

        // User ViewModels
        container.Register(() => new IdentityManagerViewModel());

        // Startup ViewModels
        container.Register(() => new IdentitySelectViewModel());
        container.Register(() => new StartupPasswordViewModel());

        // Component ViewModels
        container.Register(() => new SummaryCardViewModel());
        container.Register(() => new ChartViewModel());
        container.Register(() => new BillFilterViewModel());
        container.Register(() => new CountDownWeekViewModel());
    }

    /// <summary>
    /// Resolve a service or ViewModel by type
    /// </summary>
    public static T GetRequiredService<T>()
    {
        var service = Locator.Current.GetService<T>();
        if (service == null)
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        return service;
    }

    /// <summary>
    /// Try to resolve a service or ViewModel by type
    /// </summary>
    public static T? GetService<T>()
    {
        return Locator.Current.GetService<T>();
    }
}
