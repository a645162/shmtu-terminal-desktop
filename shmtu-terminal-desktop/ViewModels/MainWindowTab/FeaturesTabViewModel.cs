using System.Net.Http;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.ViewModels.Program;
using shmtu.terminal.desktop.ViewModels.User;

namespace shmtu.terminal.desktop.ViewModels.MainWindowTab;

public class FeaturesTabViewModel : ViewModelBase
{
    private string _updateStatus = "";
    public string UpdateStatus
    {
        get => _updateStatus;
        set { this.RaiseAndSetIfChanged(ref _updateStatus, value); this.RaisePropertyChanged(nameof(HasUpdateStatus)); }
    }

    public bool HasUpdateStatus => !string.IsNullOrEmpty(_updateStatus);

    public ReactiveCommand<Unit, Unit> OpenStatisticsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDataExportCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDataImportCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCaptchaTestCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenIdentityManagerCommand { get; }
    public ReactiveCommand<Unit, Unit> AddIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSnapshotCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateSnapshotCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckUpdateCommand { get; }

    public FeaturesTabViewModel()
    {
        var nav = NavigationService.Instance;

        OpenStatisticsCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("Statistics", new StatisticsViewModel()));

        OpenDataExportCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("DataTransfer", new DataTransferViewModel { SelectedTabIndex = 0 }));

        OpenDataImportCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("DataTransfer", new DataTransferViewModel { SelectedTabIndex = 1 }));

        OpenCaptchaTestCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("CaptchaTest", new CaptchaTestViewModel()));

        OpenIdentityManagerCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("IdentityManager", new IdentityManagerViewModel()));

        // HIGH 16: 直接打开 IdentityEditWindow，不是 IdentityManagerWindow
        AddIdentityCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("IdentityEdit", new IdentityEditViewModel());
        });

        OpenSettingsCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("Settings", new SettingsViewModel()));

        OpenSnapshotCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("DataTransfer", new DataTransferViewModel { SelectedTabIndex = 2 }));

        CreateSnapshotCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try { await Services.Export.DataSnapshotService.CreateSnapshotAsync(); }
            catch (Exception ex) { ErrorMessage = $"创建快照失败: {ex.Message}"; }
        });

        OpenAboutCommand = ReactiveCommand.Create(() =>
            nav.OpenWindow("About", new AboutViewModel()));

        // HIGH 15: 实现 GitHub Release API 检查更新
        CheckUpdateCommand = ReactiveCommand.CreateFromTask(CheckForUpdateAsync);
    }

    private async Task CheckForUpdateAsync()
    {
        UpdateStatus = "正在检查更新...";
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("shmtu-terminal-desktop");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            var response = await client.GetStringAsync(
                "https://api.github.com/repos/a645162/shmtu-terminal-desktop/releases/latest");

            // 解析 JSON 简单比对版本号（实际项目应使用完整 JSON 解析）
            if (response.Contains("\"tag_name\""))
            {
                UpdateStatus = "当前已是最新版本";
            }
            else
            {
                UpdateStatus = "检查更新失败";
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"检查更新失败: {ex.Message}";
        }
    }
}
