using Avalonia.Controls;
using Avalonia.Interactivity;
using shmtu.terminal.desktop.Models.User;
using shmtu.terminal.desktop.ViewModels.User;

namespace shmtu.terminal.desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Check Is Debug Mode
        var isDebugMode = System.Diagnostics.Debugger.IsAttached;
        // PanelDebugWindowsList.IsVisible = isDebugMode;
        if (isDebugMode)
        {
            // Show Debug Mode
            Title += " - Debug Mode";
        }
    }

    #region DebugMode

    private void ButtonUserManager_OnClick(object? sender, RoutedEventArgs e)
    {
        var userManagerWindow = new User.UserCfgWindow
        {
            DataContext = new AccountManagerViewModel()
        };
        userManagerWindow.Show();
    }

    private void ButtonAccountManager_OnClick(object? sender, RoutedEventArgs e)
    {
        var accountTest = new AccountConfigure
        {
            AccountId = "202312312345",
            Name = "Test User",
            Password = "123456"
        };
        var accountManagerWindow = new User.AccountManagerWindow
        {
            DataContext = new AccountManagerViewModel(accountTest)
        };
        accountManagerWindow.Show();
    }

    #endregion
}