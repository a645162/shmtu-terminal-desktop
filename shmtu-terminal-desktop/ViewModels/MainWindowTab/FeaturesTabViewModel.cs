using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Export;
using shmtu.terminal.desktop.Services.Navigation;

namespace shmtu.terminal.desktop.ViewModels.MainWindowTab;

/// <summary>
/// ViewModel for the Features tab (3x3 grid of feature cards)
/// XAML bindings: OpenStatisticsCommand, OpenDataExportCommand, OpenDataImportCommand,
///   OpenCaptchaTestCommand, OpenIdentityManagerCommand, AddIdentityCommand,
///   OpenSettingsCommand, OpenSnapshotCommand, CreateSnapshotCommand,
///   OpenAboutCommand, CheckUpdateCommand
/// </summary>
public class FeaturesTabViewModel : ViewModelBase
{
    private string _errorMessage = "";
    public new string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

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
        {
            // Navigate to bill tab with statistics view
            nav.SwitchTab(1);
        });

        OpenDataExportCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("DataTransfer", new ViewModels.Program.DataTransferViewModel { SelectedTabIndex = 0 });
        });

        OpenDataImportCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("DataTransfer", new ViewModels.Program.DataTransferViewModel { SelectedTabIndex = 1 });
        });

        OpenCaptchaTestCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("CaptchaTest", new ViewModels.Program.CaptchaTestViewModel());
        });

        OpenIdentityManagerCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("IdentityManager", new ViewModels.User.IdentityManagerViewModel());
        });

        AddIdentityCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("IdentityManager", new ViewModels.User.IdentityManagerViewModel());
        });

        OpenSettingsCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("Settings", new ViewModels.Program.SettingsViewModel());
        });

        OpenSnapshotCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("DataTransfer", new ViewModels.Program.DataTransferViewModel { SelectedTabIndex = 2 });
        });

        CreateSnapshotCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await DataSnapshotService.CreateSnapshotAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"创建快照失败: {ex.Message}";
            }
        });

        OpenAboutCommand = ReactiveCommand.Create(() =>
        {
            nav.OpenWindow("About", new ViewModels.Program.AboutViewModel());
        });

        CheckUpdateCommand = ReactiveCommand.Create(() =>
        {
            // TODO: Implement update check
        });
    }
}
