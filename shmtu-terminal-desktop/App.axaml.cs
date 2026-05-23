using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Services;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.ViewModels;
using shmtu.terminal.desktop.ViewModels.Startup;
using shmtu.terminal.desktop.Views;
using shmtu.terminal.desktop.Views.Startup;

namespace shmtu.terminal.desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        LoggingService.Debug("加载 App XAML 资源");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LoggingService.Debug("框架初始化完成，进入应用生命周期");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            LoggingService.Information("桌面应用生命周期已启动");

            // Initialize database
            LoggingService.Information("初始化数据库...");
            InitDb.Init();
            LoggingService.Information("数据库初始化完成");

            // Initialize service locator (DI container)
            LoggingService.Information("初始化服务定位器...");
            ServiceLocator.Initialize();
            LoggingService.Information("服务定位器初始化完成");

            // Determine startup flow
            var config = TomlConfigService.LoadAppConfig();
            LoggingService.LogConfigLoad("app_config", true);

            if (config.Security.EnableStartupProtection && !string.IsNullOrEmpty(config.Security.PasswordHash))
            {
                LoggingService.Information("检测到启动密码保护启用");
                // Show startup password window first
                var passwordViewModel = new StartupPasswordViewModel();
                var passwordWindow = new StartupPasswordWindow
                {
                    DataContext = passwordViewModel,
                };

                passwordViewModel.PasswordVerified += () =>
                {
                    // Password verified, proceed to identity selection
                    LoggingService.Information("密码验证成功，进入身份选择");
                    passwordWindow.Close();
                    ShowIdentitySelectionOrMainWindow(desktop);
                };

                desktop.MainWindow = passwordWindow;
            }
            else
            {
                LoggingService.Debug("未启用启动密码保护，直接进入身份选择");
                // No password protection, proceed directly
                ShowIdentitySelectionOrMainWindow(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Show identity selection if multiple identities exist and no default,
    /// otherwise go straight to main window
    /// </summary>
    private void ShowIdentitySelectionOrMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        LoggingService.Debug("进入身份选择或主窗口流程");

        var config = TomlConfigService.LoadAppConfig();
        var identities = Database.Manage.Identity.IdentityDb.GetEnabled();
        LoggingService.Debug("获取到 {Count} 个已启用的身份", identities.Count);

        if (config.Identity.RememberDefault && config.Identity.DefaultIdentityId > 0)
        {
            var defaultIdentity = Database.Manage.Identity.IdentityDb.GetById(config.Identity.DefaultIdentityId);
            if (defaultIdentity != null && defaultIdentity.Enable)
            {
                LoggingService.Information("使用默认身份自动登录 | IdentityId={IdentityId} | Name={Name}",
                    defaultIdentity.Id, defaultIdentity.Name);
                // Auto-enter with default identity
                ShowMainWindow(desktop, defaultIdentity.Id);
                return;
            }
        }

        if (identities.Count <= 1)
        {
            // Only one identity or no identity, enter directly
            var identityId = identities.Count > 0 ? identities[0].Id : 0;
            LoggingService.Information("只有 {Count} 个身份，直接进入主窗口", identities.Count);
            ShowMainWindow(desktop, identityId);
            return;
        }

        // Multiple identities and no default — show selection window
        LoggingService.Information("显示身份选择窗口 | IdentityCount={Count}", identities.Count);
        var selectViewModel = new IdentitySelectViewModel();
        var selectWindow = new IdentitySelectWindow
        {
            DataContext = selectViewModel,
        };

        selectViewModel.IdentitySelected += (identityId) =>
        {
            LoggingService.Information("用户选择身份 | IdentityId={IdentityId}", identityId);
            selectWindow.Close();
            ShowMainWindow(desktop, identityId);
        };

        desktop.MainWindow = selectWindow;
    }

    /// <summary>
    /// Show the main window with the specified identity
    /// </summary>
    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop, int identityId)
    {
        LoggingService.Information("创建主窗口 | IdentityId={IdentityId}", identityId);

        var mainWindowViewModel = new MainWindowViewModel();
        mainWindowViewModel.InitializeWithIdentity(identityId);

        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel,
        };

        NavigationService.Instance.SetMainWindow(mainWindow);
        desktop.MainWindow = mainWindow;

        LoggingService.Information("主窗口创建完成并显示");
    }
}
