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

    private NavigationService()
    {
        LoggingService.Debug("[NavigationService] NavigationService 单例创建");
    }

    /// <summary>
    /// 设置主窗口
    /// </summary>
    public void SetMainWindow(Window window)
    {
        LoggingService.Debug("[Navigation] 设置主窗口 | WindowType={Type}", window.GetType().Name);
        _mainWindow = window;
    }

    /// <summary>
    /// 打开新窗口（泛型版本）
    /// </summary>
    public void OpenWindow<TWindow>(object? viewModel = null) where TWindow : Window, new()
    {
        LoggingService.Debug("[Navigation] 打开窗口（泛型）| WindowType={Type} | HasViewModel={HasVM}",
            typeof(TWindow).Name, viewModel != null);

        Dispatcher.UIThread.Post(() =>
        {
            var window = new TWindow();
            if (viewModel != null)
            {
                LoggingService.Debug("[Navigation] 设置 ViewModel | Type={Type}", viewModel.GetType().Name);
                window.DataContext = viewModel;
            }
            if (_mainWindow != null)
            {
                LoggingService.Debug("[Navigation] 显示为对话框 | MainWindow={Main}", _mainWindow.GetType().Name);
                window.ShowDialog(_mainWindow);
            }
            else
            {
                LoggingService.Debug("[Navigation] 无主窗口，直接显示");
                window.Show();
            }
        });
    }

    /// <summary>
    /// 打开特定类型窗口
    /// </summary>
    public void OpenWindow(string windowType, object? viewModel = null)
    {
        LoggingService.Debug("[Navigation] 打开窗口（字符串）| WindowType={Type} | HasViewModel={HasVM}",
            windowType, viewModel != null);

        Dispatcher.UIThread.Post(() =>
        {
            Window? window = windowType switch
            {
                "Settings" => new Views.Program.SettingsWindow(),
                "About" => new Views.Program.AboutWindow(),
                "DataTransfer" => new Views.Program.DataTransferWindow(),
                "Statistics" => new Views.Program.StatisticsWindow(),
                "CaptchaTest" => new Views.Program.CaptchaTestWindow(),
                "IdentityManager" => new Views.User.IdentityManagerWindow(),
                "IdentitySelect" => new Views.Startup.IdentitySelectWindow(),
                "IdentityEdit" => new Views.User.IdentityEditWindow(),
                "AccountEdit" => new Views.User.AccountEditWindow(),
                "BillDetail" => new Views.Component.Bill.BillDetailWindow(),
                "BillMerge" => new Views.Component.Bill.BillMergeWindow(),
                _ => null,
            };

            if (window == null)
            {
                LoggingService.Warning("[Navigation] 未知的窗口类型 | WindowType={Type}", windowType);
                return;
            }

            if (viewModel != null)
            {
                LoggingService.Debug("[Navigation] 设置 ViewModel | Type={Type}", viewModel.GetType().Name);
                window.DataContext = viewModel;
            }

            if (_mainWindow != null)
            {
                LoggingService.Debug("[Navigation] 显示为对话框");
                window.ShowDialog(_mainWindow);
            }
            else
            {
                LoggingService.Warning("[Navigation] 无主窗口，直接显示窗口");
                window.Show();
            }
        });
    }

    /// <summary>
    /// 切换标签页
    /// </summary>
    public void SwitchTab(int tabIndex)
    {
        LoggingService.Debug("[Navigation] 切换标签页 | Index={Index}", tabIndex);

        if (_mainWindow?.DataContext is ViewModels.MainWindowViewModel vm)
        {
            Dispatcher.UIThread.Post(() =>
            {
                vm.SelectedTabIndex = tabIndex;
                LoggingService.Information("[Navigation] 标签页切换完成 | Index={Index}", tabIndex);
            });
        }
        else
        {
            LoggingService.Warning("[Navigation] 无法切换标签页，主窗口或 ViewModel 无效");
        }
    }

    /// <summary>
    /// 替换主窗口
    /// </summary>
    public void ReplaceMainWindow(Window newWindow)
    {
        LoggingService.Debug("[Navigation] 替换主窗口 | OldType={Old} | NewType={New}",
            _mainWindow?.GetType().Name, newWindow.GetType().Name);

        Dispatcher.UIThread.Post(() =>
        {
            var oldWindow = _mainWindow;
            _mainWindow = newWindow;
            newWindow.Show();
            oldWindow?.Close();
            LoggingService.Information("[Navigation] 主窗口替换完成");
        });
    }

    /// <summary>
    /// 显示保存文件对话框
    /// </summary>
    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string[]? filters = null)
    {
        LoggingService.Debug("[Navigation] 显示保存文件对话框 | Title={Title} | DefaultFile={File}",
            title, defaultFileName);

        if (_mainWindow == null)
        {
            LoggingService.Warning("[Navigation] 无法显示对话框，无主窗口");
            return null;
        }

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeFilter = filters != null ? ConvertFilters(filters) : null,
        };
        var result = await _mainWindow.StorageProvider.SaveFilePickerAsync(options);

        if (result != null)
        {
            LoggingService.Information("[Navigation] 文件保存路径已选择 | Path={Path}", result.Path.LocalPath);
        }
        else
        {
            LoggingService.Debug("[Navigation] 文件保存对话框已取消");
        }

        return result?.Path.LocalPath;
    }

    /// <summary>
    /// 显示打开文件对话框
    /// </summary>
    public async Task<string[]> ShowOpenFileDialogAsync(string title, string[]? filters = null, bool allowMultiple = false)
    {
        LoggingService.Debug("[Navigation] 显示打开文件对话框 | Title={Title} | Multiple={Multiple}",
            title, allowMultiple);

        if (_mainWindow == null)
        {
            LoggingService.Warning("[Navigation] 无法显示对话框，无主窗口");
            return [];
        }

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = filters != null ? ConvertFilters(filters) : null,
        };
        var result = await _mainWindow.StorageProvider.OpenFilePickerAsync(options);

        var paths = result.Select(r => r.Path.LocalPath).ToArray();
        LoggingService.Information("[Navigation] 已选择 {Count} 个文件", paths.Length);

        return paths;
    }

    /// <summary>
    /// 将扩展名数组转换为 Avalonia FilePickerFileType 列表
    /// </summary>
    private static List<FilePickerFileType> ConvertFilters(string[] filters)
    {
        return filters.Select(ext => new FilePickerFileType($"{ext.ToUpper()} 文件")
        {
            Patterns = [$"*.{ext}"],
        }).ToList();
    }

    /// <summary>
    /// 显示打开文件夹对话框
    /// </summary>
    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        LoggingService.Debug("[Navigation] 显示打开文件夹对话框 | Title={Title}", title);

        if (_mainWindow == null)
        {
            LoggingService.Warning("[Navigation] 无法显示对话框，无主窗口");
            return null;
        }

        var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
        var result = await _mainWindow.StorageProvider.OpenFolderPickerAsync(options);

        var path = result.FirstOrDefault()?.Path.LocalPath;
        if (path != null)
        {
            LoggingService.Information("[Navigation] 文件夹已选择 | Path={Path}", path);
        }
        else
        {
            LoggingService.Debug("[Navigation] 文件夹选择已取消");
        }

        return path;
    }
}
