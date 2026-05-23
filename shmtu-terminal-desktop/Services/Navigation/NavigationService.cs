using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace shmtu.terminal.desktop.Services.Navigation;

/// <summary>
/// Navigation service — manages window navigation, tab switching, and startup flow
/// </summary>
public class NavigationService
{
    private static NavigationService? _instance;
    public static NavigationService Instance => _instance ??= new NavigationService();

    private Window? _mainWindow;

    /// <summary>
    /// Currently selected identity ID for the session
    /// </summary>
    public int CurrentIdentityId { get; set; }

    /// <summary>
    /// Set the main window reference
    /// </summary>
    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    /// <summary>
    /// Open a new window as a child of the main window
    /// </summary>
    public void OpenWindow<TWindow>(object? viewModel = null) where TWindow : Window, new()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new TWindow();
            if (viewModel != null)
            {
                window.DataContext = viewModel;
            }

            if (_mainWindow != null)
            {
                window.ShowDialog(_mainWindow);
            }
            else
            {
                window.Show();
            }
        });
    }

    /// <summary>
    /// Open a specific window type by name
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
                _ => null,
            };

            if (window == null) return;

            if (viewModel != null)
            {
                window.DataContext = viewModel;
            }

            if (_mainWindow != null)
            {
                window.ShowDialog(_mainWindow);
            }
            else
            {
                window.Show();
            }
        });
    }

    /// <summary>
    /// Switch tab in main window
    /// </summary>
    public void SwitchTab(int tabIndex)
    {
        if (_mainWindow?.DataContext is ViewModels.MainWindowViewModel vm)
        {
            Dispatcher.UIThread.Post(() => vm.SelectedTabIndex = tabIndex);
        }
    }

    /// <summary>
    /// Close the main window and show a new one (for startup flow)
    /// </summary>
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

    /// <summary>
    /// Show a save file dialog
    /// </summary>
    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string[]? filters = null)
    {
        if (_mainWindow == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
        };

        var result = await _mainWindow.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    /// <summary>
    /// Show an open file dialog
    /// </summary>
    public async Task<string[]> ShowOpenFileDialogAsync(string title, string[]? filters = null, bool allowMultiple = false)
    {
        if (_mainWindow == null) return [];

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
        };

        var result = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);
        return result.Select(r => r.Path.LocalPath).ToArray();
    }

    /// <summary>
    /// Show an open folder dialog
    /// </summary>
    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        if (_mainWindow == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };

        var result = await _mainWindow.StorageProvider.OpenFolderPickerAsync(options);
        return result.FirstOrDefault()?.Path.LocalPath;
    }
}
