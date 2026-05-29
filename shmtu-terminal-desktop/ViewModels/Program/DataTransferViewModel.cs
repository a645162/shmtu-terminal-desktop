using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Export;
using shmtu.terminal.desktop.Services.Navigation;

namespace shmtu.terminal.desktop.ViewModels.Program;

/// <summary>
/// Snapshot display item for the DataGrid
/// </summary>
public class SnapshotDisplayItem : ViewModelBase
{
    public string SnapshotTime { get; set; } = "";
    public string SizeText { get; set; } = "";
    public string FilePath { get; set; } = "";

    public ReactiveCommand<Unit, Unit> RestoreCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    public SnapshotDisplayItem(Action<SnapshotDisplayItem> restoreAction, Action<SnapshotDisplayItem> deleteAction)
    {
        RestoreCommand = ReactiveCommand.Create(() => restoreAction(this));
        DeleteCommand = ReactiveCommand.Create(() => deleteAction(this));
    }
}

/// <summary>
/// ViewModel for the Data Transfer window (Export/Import/Snapshot)
/// XAML bindings: SelectedTabIndex, IsExportTab, IsImportTab, IsSnapshotTab,
///   Export tab, Import tab, Snapshot tab
/// </summary>
public class DataTransferViewModel : ViewModelBase
{
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
            this.RaisePropertyChanged(nameof(IsExportTab));
            this.RaisePropertyChanged(nameof(IsImportTab));
            this.RaisePropertyChanged(nameof(IsSnapshotTab));
        }
    }

    public bool IsExportTab => _selectedTabIndex == 0;
    public bool IsImportTab => _selectedTabIndex == 1;
    public bool IsSnapshotTab => _selectedTabIndex == 2;

    // Export tab
    public ObservableCollection<string> ExportDataScopes { get; } = ["全部数据", "本月数据", "近30天", "近90天"];

    private string _selectedExportScope = "全部数据";
    public string SelectedExportScope
    {
        get => _selectedExportScope;
        set => this.RaiseAndSetIfChanged(ref _selectedExportScope, value);
    }

    public ObservableCollection<string> IdentityList { get; } = [];

    private string _selectedExportIdentity = "";
    public string SelectedExportIdentity
    {
        get => _selectedExportIdentity;
        set => this.RaiseAndSetIfChanged(ref _selectedExportIdentity, value);
    }

    private bool _isCsvFormat = true;
    public bool IsCsvFormat
    {
        get => _isCsvFormat;
        set => this.RaiseAndSetIfChanged(ref _isCsvFormat, value);
    }

    private bool _isJsonFormat;
    public bool IsJsonFormat
    {
        get => _isJsonFormat;
        set => this.RaiseAndSetIfChanged(ref _isJsonFormat, value);
    }

    private bool _isQianjiFormat;
    public bool IsQianjiFormat
    {
        get => _isQianjiFormat;
        set => this.RaiseAndSetIfChanged(ref _isQianjiFormat, value);
    }

    private string _exportPath = "";
    public string ExportPath
    {
        get => _exportPath;
        set => this.RaiseAndSetIfChanged(ref _exportPath, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseExportPathCommand { get; }
    public ReactiveCommand<Unit, Unit> StartExportCommand { get; }

    private bool _isExporting;
    public bool IsExporting
    {
        get => _isExporting;
        set => this.RaiseAndSetIfChanged(ref _isExporting, value);
    }

    private bool _isExportIndeterminate;
    public bool IsExportIndeterminate
    {
        get => _isExportIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _isExportIndeterminate, value);
    }

    private double _exportProgress;
    public double ExportProgress
    {
        get => _exportProgress;
        set => this.RaiseAndSetIfChanged(ref _exportProgress, value);
    }

    private string _exportStatus = "";
    public string ExportStatus
    {
        get => _exportStatus;
        set => this.RaiseAndSetIfChanged(ref _exportStatus, value);
    }

    // Import tab
    private string _importFilePath = "";
    public string ImportFilePath
    {
        get => _importFilePath;
        set => this.RaiseAndSetIfChanged(ref _importFilePath, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseImportFileCommand { get; }

    private string _selectedImportIdentity = "";
    public string SelectedImportIdentity
    {
        get => _selectedImportIdentity;
        set => this.RaiseAndSetIfChanged(ref _selectedImportIdentity, value);
    }

    private bool _hasImportPreview;
    public bool HasImportPreview
    {
        get => _hasImportPreview;
        set => this.RaiseAndSetIfChanged(ref _hasImportPreview, value);
    }

    private string _importPreviewInfo = "";
    public string ImportPreviewInfo
    {
        get => _importPreviewInfo;
        set => this.RaiseAndSetIfChanged(ref _importPreviewInfo, value);
    }

    public ReactiveCommand<Unit, Unit> PreviewImportCommand { get; }
    public ReactiveCommand<Unit, Unit> StartImportCommand { get; }

    // Snapshot tab
    public ReactiveCommand<Unit, Unit> CreateSnapshotCommand { get; }

    private int _snapshotKeepCount = 10;
    public int SnapshotKeepCount
    {
        get => _snapshotKeepCount;
        set => this.RaiseAndSetIfChanged(ref _snapshotKeepCount, value);
    }

    public ObservableCollection<SnapshotDisplayItem> Snapshots { get; } = [];

    public DataTransferViewModel()
    {
        var nav = NavigationService.Instance;

        LoadIdentityList();
        LoadSnapshots();

        // Load snapshot keep count from config
        var config = TomlConfigService.LoadAppConfig();
        SnapshotKeepCount = config.Data.SnapshotKeepCount;

        // Export commands
        BrowseExportPathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var format = IsCsvFormat ? "csv" : IsQianjiFormat ? "json" : "json";
            var result = await nav.ShowSaveFileDialogAsync("选择导出路径", $"export_{DateTime.Now:yyyyMMdd}.{format}", [format]);
            if (result != null)
                ExportPath = result;
        });

        StartExportCommand = ReactiveCommand.CreateFromTask(StartExportAsync);

        // Import commands
        BrowseImportFileCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await nav.ShowOpenFileDialogAsync("选择导入文件", ["json", "csv"]);
            if (result.Length > 0)
                ImportFilePath = result[0];
        });

        PreviewImportCommand = ReactiveCommand.Create(PreviewImport);
        StartImportCommand = ReactiveCommand.CreateFromTask(StartImportAsync);

        // Snapshot commands
        CreateSnapshotCommand = ReactiveCommand.CreateFromTask(CreateSnapshotAsync);
    }

    private void LoadIdentityList()
    {
        IdentityList.Clear();
        var identities = Database.Manage.Identity.IdentityDb.GetAll();
        foreach (var identity in identities)
        {
            IdentityList.Add(identity.Name);
        }
    }

    private async Task StartExportAsync()
    {
        if (string.IsNullOrEmpty(ExportPath)) return;

        IsExporting = true;
        IsExportIndeterminate = true;
        ExportStatus = "正在导出...";

        try
        {
            var identityId = GetIdentityIdByName(SelectedExportIdentity);
            if (identityId <= 0) return;

            var format = IsCsvFormat ? ExportFormat.Csv
                : IsQianjiFormat ? ExportFormat.Qianji
                : ExportFormat.Json;

            long? startTs = null, endTs = null;
            var now = DateTime.Now;
            switch (SelectedExportScope)
            {
                case "本月数据":
                    startTs = ((DateTimeOffset)new DateTime(now.Year, now.Month, 1)).ToUnixTimeSeconds();
                    endTs = ((DateTimeOffset)now).ToUnixTimeSeconds();
                    break;
                case "近30天":
                    startTs = ((DateTimeOffset)now.AddDays(-30)).ToUnixTimeSeconds();
                    endTs = ((DateTimeOffset)now).ToUnixTimeSeconds();
                    break;
                case "近90天":
                    startTs = ((DateTimeOffset)now.AddDays(-90)).ToUnixTimeSeconds();
                    endTs = ((DateTimeOffset)now).ToUnixTimeSeconds();
                    break;
            }

            IsExportIndeterminate = false;

            if (startTs.HasValue && endTs.HasValue)
            {
                await BillExportService.ExportAsync(identityId, SelectedExportIdentity, format, ExportPath, startTs.Value, endTs.Value);
            }
            else
            {
                await BillExportService.ExportAsync(identityId, SelectedExportIdentity, format, ExportPath);
            }

            ExportStatus = "导出完成!";
            ExportProgress = 100;
        }
        catch (Exception ex)
        {
            ExportStatus = $"导出失败: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void PreviewImport()
    {
        if (string.IsNullOrEmpty(ImportFilePath)) return;

        try
        {
            var fileInfo = new System.IO.FileInfo(ImportFilePath);
            ImportPreviewInfo = $"文件大小: {fileInfo.Length / 1024.0:F1} KB\n文件格式: {fileInfo.Extension}";
            HasImportPreview = true;
        }
        catch (Exception ex)
        {
            ImportPreviewInfo = $"预览失败: {ex.Message}";
            HasImportPreview = true;
        }
    }

    private async Task StartImportAsync()
    {
        if (string.IsNullOrEmpty(ImportFilePath)) return;

        var identityId = GetIdentityIdByName(SelectedImportIdentity);
        if (identityId <= 0) return;

        try
        {
            var ext = System.IO.Path.GetExtension(ImportFilePath).ToLower();
            BillImportService.ImportResult result;

            if (ext == ".csv")
            {
                result = await BillImportService.ImportCsvAsync(identityId, ImportFilePath);
            }
            else
            {
                result = await BillImportService.ImportJsonAsync(identityId, ImportFilePath);
            }

            HasImportPreview = true;
            ImportPreviewInfo = $"导入完成: {result.ImportedCount}条导入, {result.SkippedCount}条跳过" +
                                (result.Errors.Count > 0 ? $"\n{result.Errors.Count}个错误" : "");
        }
        catch (Exception ex)
        {
            HasImportPreview = true;
            ImportPreviewInfo = $"导入失败: {ex.Message}";
        }
    }

    private async Task CreateSnapshotAsync()
    {
        try
        {
            await DataSnapshotService.CreateSnapshotAsync();

            // Update keep count in config
            TomlConfigService.UpdateAppConfig(config =>
            {
                config.Data.SnapshotKeepCount = SnapshotKeepCount;
            });

            LoadSnapshots();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"创建快照失败: {ex.Message}";
        }
    }

    private void LoadSnapshots()
    {
        Snapshots.Clear();
        var snapshots = DataSnapshotService.GetSnapshotList();
        foreach (var snapshot in snapshots)
        {
            Snapshots.Add(new SnapshotDisplayItem(RestoreSnapshot, DeleteSnapshotItem)
            {
                SnapshotTime = snapshot.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                SizeText = snapshot.SizeText,
                FilePath = snapshot.FilePath,
            });
        }
    }

    private async void RestoreSnapshot(SnapshotDisplayItem item)
    {
        try
        {
            await DataSnapshotService.RestoreSnapshotAsync(item.FilePath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"恢复快照失败: {ex.Message}";
        }
    }

    private void DeleteSnapshotItem(SnapshotDisplayItem item)
    {
        try
        {
            DataSnapshotService.DeleteSnapshot(item.FilePath);
            Snapshots.Remove(item);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除快照失败: {ex.Message}";
        }
    }

    private int GetIdentityIdByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        var identity = Database.Manage.Identity.IdentityDb.GetAll()
            .FirstOrDefault(i => i.Name == name);
        return identity?.Id ?? 0;
    }
}
