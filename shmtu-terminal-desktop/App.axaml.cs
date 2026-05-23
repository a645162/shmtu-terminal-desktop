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
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Initialize database
            InitDb.Init();

            // Initialize service locator (DI container)
            ServiceLocator.Initialize();

            // Determine startup flow
            var config = TomlConfigService.LoadAppConfig();

            if (config.Security.EnableStartupProtection && !string.IsNullOrEmpty(config.Security.PasswordHash))
            {
                // Show startup password window first
                var passwordViewModel = new StartupPasswordViewModel();
                var passwordWindow = new StartupPasswordWindow
                {
                    DataContext = passwordViewModel,
                };

                passwordViewModel.PasswordVerified += () =>
                {
                    // Password verified, proceed to identity selection
                    passwordWindow.Close();
                    ShowIdentitySelectionOrMainWindow(desktop);
                };

                desktop.MainWindow = passwordWindow;
            }
            else
            {
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
        var config = TomlConfigService.LoadAppConfig();
        var identities = Database.Manage.Identity.IdentityDb.GetEnabled();

        if (config.Identity.RememberDefault && config.Identity.DefaultIdentityId > 0)
        {
            var defaultIdentity = Database.Manage.Identity.IdentityDb.GetById(config.Identity.DefaultIdentityId);
            if (defaultIdentity != null && defaultIdentity.Enable)
            {
                // Auto-enter with default identity
                ShowMainWindow(desktop, defaultIdentity.Id);
                return;
            }
        }

        if (identities.Count <= 1)
        {
            // Only one identity or no identity, enter directly
            var identityId = identities.Count > 0 ? identities[0].Id : 0;
            ShowMainWindow(desktop, identityId);
            return;
        }

        // Multiple identities and no default — show selection window
        var selectViewModel = new IdentitySelectViewModel();
        var selectWindow = new IdentitySelectWindow
        {
            DataContext = selectViewModel,
        };

        selectViewModel.IdentitySelected += (identityId) =>
        {
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
        var mainWindowViewModel = new MainWindowViewModel();
        mainWindowViewModel.InitializeWithIdentity(identityId);

        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel,
        };

        NavigationService.Instance.SetMainWindow(mainWindow);
        desktop.MainWindow = mainWindow;
    }
}
