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
        if (_isInitialized)
        {
            LoggingService.Warning("[ServiceLocator] 已初始化，忽略重复调用");
            return;
        }

        LoggingService.Information("[ServiceLocator] 开始初始化服务定位器");
        _isInitialized = true;

        var container = Locator.CurrentMutable;

        // Register services
        LoggingService.Debug("[ServiceLocator] 注册服务...");
        RegisterServices(container);
        LoggingService.Debug("[ServiceLocator] 服务注册完成");

        // Register ViewModels
        LoggingService.Debug("[ServiceLocator] 注册 ViewModels...");
        RegisterViewModels(container);
        LoggingService.Debug("[ServiceLocator] ViewModels 注册完成");

        LoggingService.Information("[ServiceLocator] 服务定位器初始化完成");
    }

    private static void RegisterServices(IMutableDependencyResolver container)
    {
        LoggingService.Debug("[ServiceLocator] 注册 NavigationService（单例）");
        container.RegisterLazySingleton(() => NavigationService.Instance);

        LoggingService.Debug("[ServiceLocator] 注册 BillSyncService（每次新实例）");
        container.Register(() => new BillSyncService());

        LoggingService.Debug("[ServiceLocator] 注册 BillClassifier（每次新实例）");
        container.Register(() => new BillClassifier());

        LoggingService.Information("[ServiceLocator] 已注册服务: NavigationService, BillSyncService, BillClassifier");
    }

    private static void RegisterViewModels(IMutableDependencyResolver container)
    {
        // Main window ViewModel
        LoggingService.Debug("[ServiceLocator] 注册 MainWindowViewModel（单例）");
        container.RegisterLazySingleton(() => new MainWindowViewModel());

        // Tab ViewModels
        LoggingService.Debug("[ServiceLocator] 注册 Tab ViewModels");
        container.Register(() => new HomeTabViewModel());
        container.Register(() => new BillTabViewModel());
        container.Register(() => new FeaturesTabViewModel());

        // Program ViewModels
        LoggingService.Debug("[ServiceLocator] 注册 Program ViewModels");
        container.Register(() => new SettingsViewModel());
        container.Register(() => new AboutViewModel());
        container.Register(() => new DataTransferViewModel());
        container.Register(() => new CaptchaTestViewModel());

        // User ViewModels
        LoggingService.Debug("[ServiceLocator] 注册 User ViewModels");
        container.Register(() => new IdentityManagerViewModel());

        // Startup ViewModels
        LoggingService.Debug("[ServiceLocator] 注册 Startup ViewModels");
        container.Register(() => new IdentitySelectViewModel());
        container.Register(() => new StartupPasswordViewModel());

        // Component ViewModels
        LoggingService.Debug("[ServiceLocator] 注册 Component ViewModels");
        container.Register(() => new SummaryCardViewModel());
        container.Register(() => new ChartViewModel());
        container.Register(() => new BillFilterViewModel());
        container.Register(() => new CountDownWeekViewModel());

        LoggingService.Information("[ServiceLocator] 已注册 {Count} 个 ViewModels",
            2 + 3 + 4 + 1 + 2 + 4 + 2); // 计算注册的 ViewModel 数量
    }

    /// <summary>
    /// Resolve a service or ViewModel by type
    /// </summary>
    public static T GetRequiredService<T>()
    {
        LoggingService.Debug("[ServiceLocator] 获取必需服务 | Type={Type}", typeof(T).Name);
        var service = Locator.Current.GetService<T>();
        if (service == null)
        {
            LoggingService.Error("[ServiceLocator] 服务未注册 | Type={Type}", typeof(T).Name);
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }
        LoggingService.Debug("[ServiceLocator] 服务解析成功 | Type={Type}", typeof(T).Name);
        return service;
    }

    /// <summary>
    /// Try to resolve a service or ViewModel by type
    /// </summary>
    public static T? GetService<T>()
    {
        LoggingService.Verbose("[ServiceLocator] 尝试解析服务 | Type={Type}", typeof(T).Name);
        return Locator.Current.GetService<T>();
    }
}
