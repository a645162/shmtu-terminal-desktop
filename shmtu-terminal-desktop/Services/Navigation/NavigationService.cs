using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace shmtu.terminal.desktop.Services.Navigation;

public class NavigationService
{
    private static NavigationService? _instance;
    public static NavigationService Instance => _instance ??= new NavigationService();

    private Window? _mainWindow;

    public int CurrentIdentityId { get; set; }

    public void SetMainWindow(Window window) => _mainWindow = window;

    /// <summary>
    /// Open a new window as a child of the main window
    /// </summary>
    public void OpenWindow<TWindow>(object? viewModel = null) where TWindow : Window, new()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new TWindow();
            if (viewModel != null) window.DataContext = viewModel;
            if (_mainWindow != null) window.ShowDialog(_mainWindow);
            else window.Show();
        });
    }

    /// <summary>
    /// Open a specific window type by name — includes new dialog windows
    /// </summary>
    public void OpenWindow(string windowType, object? viewModel = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Window? window = windowType switch
            {
                "Settings" => new Views.Program.SettingsWindow(),
                "About" => new Views.Program.AboutWindow(),
                "DataTransfer" => new Views.Program.DataTransferWindow(),
                "CaptchaTest" => new Views.Program.CaptchaTestWindow(),
                "IdentityManager" => new Views.User.IdentityManagerWindow(),
                "IdentitySelect" => new Views.Startup.IdentitySelectWindow(),
                "IdentityEdit" => new Views.User.IdentityEditWindow(),
                "AccountEdit" => new Views.User.AccountEditWindow(),
                // HIGH 6: 注册新窗口类型
                "BillDetail" => new Views.Component.Captcha.ManualCaptchaWindow(),
                "BillMerge" => new Views.Component.Captcha.ManualCaptchaWindow(),
                _ => null,
            };

            if (window == null) return;
            if (viewModel != null) window.DataContext = viewModel;
            if (_mainWindow != null) window.ShowDialog(_mainWindow);
            else window.Show();
        });
    }

    public void SwitchTab(int tabIndex)
    {
        if (_mainWindow?.DataContext is ViewModels.MainWindowViewModel vm)
            Dispatcher.UIThread.Post(() => vm.SelectedTabIndex = tabIndex);
    }

    public void ReplaceMainWindow(Window newWindow)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var oldWindow = _mainWindow;
            _mainWindow = newWindow;
            newWindow.Show();
            oldWindow?.Close();
        });
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string[]? filters = null)
    {
        if (_mainWindow == null) return null;
        var options = new FilePickerSaveOptions { Title = title, SuggestedFileName = defaultFileName };
        var result = await _mainWindow.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    public async Task<string[]> ShowOpenFileDialogAsync(string title, string[]? filters = null, bool allowMultiple = false)
    {
        if (_mainWindow == null) return [];
        var options = new FilePickerOpenOptions { Title = title, AllowMultiple = allowMultiple };
        var result = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);
        return result.Select(r => r.Path.LocalPath).ToArray();
    }

    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        if (_mainWindow == null) return null;
        var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
        var result = await _mainWindow.StorageProvider.OpenFolderPickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }
}
