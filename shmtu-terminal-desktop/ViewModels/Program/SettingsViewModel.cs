using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;

namespace shmtu.terminal.desktop.ViewModels.Program;

/// <summary>
/// ViewModel for the Settings window
/// XAML bindings: SelectedSettingsIndex, SettingsCategories, Is*Selected,
///   Security settings, Captcha settings, Sync settings, Data settings,
///   UI settings, Classification settings, Update settings,
///   ResetDefaultsCommand, CancelCommand, SaveCommand
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    // Navigation
    public ObservableCollection<string> SettingsCategories { get; } =
        ["安全设置", "验证码设置", "同步设置", "数据设置", "界面设置", "分类规则", "更新设置"];

    private int _selectedSettingsIndex;
    public int SelectedSettingsIndex
    {
        get => _selectedSettingsIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSettingsIndex, value);
            this.RaisePropertyChanged(nameof(IsSecuritySelected));
            this.RaisePropertyChanged(nameof(IsCaptchaSelected));
            this.RaisePropertyChanged(nameof(IsSyncSelected));
            this.RaisePropertyChanged(nameof(IsDataSelected));
            this.RaisePropertyChanged(nameof(IsUiSelected));
            this.RaisePropertyChanged(nameof(IsClassificationSelected));
            this.RaisePropertyChanged(nameof(IsUpdateSelected));
        }
    }

    public bool IsSecuritySelected => _selectedSettingsIndex == 0;
    public bool IsCaptchaSelected => _selectedSettingsIndex == 1;
    public bool IsSyncSelected => _selectedSettingsIndex == 2;
    public bool IsDataSelected => _selectedSettingsIndex == 3;
    public bool IsUiSelected => _selectedSettingsIndex == 4;
    public bool IsClassificationSelected => _selectedSettingsIndex == 5;
    public bool IsUpdateSelected => _selectedSettingsIndex == 6;

    // Security Settings
    private bool _enableStartupProtection;
    public bool EnableStartupProtection
    {
        get => _enableStartupProtection;
        set => this.RaiseAndSetIfChanged(ref _enableStartupProtection, value);
    }

    private string _protectionPassword = "";
    public string ProtectionPassword
    {
        get => _protectionPassword;
        set => this.RaiseAndSetIfChanged(ref _protectionPassword, value);
    }

    public ReactiveCommand<Unit, Unit> SetPasswordCommand { get; }

    // Captcha Settings
    public ObservableCollection<string> CaptchaModes { get; } = ["手动输入", "远程OCR", "本地ONNX"];

    private string _selectedCaptchaMode = "手动输入";
    public string SelectedCaptchaMode
    {
        get => _selectedCaptchaMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCaptchaMode, value);
            this.RaisePropertyChanged(nameof(IsRemoteOcrSelected));
            this.RaisePropertyChanged(nameof(IsLocalOnnxSelected));
        }
    }

    public bool IsRemoteOcrSelected => SelectedCaptchaMode == "远程OCR";
    public bool IsLocalOnnxSelected => SelectedCaptchaMode == "本地ONNX";

    private string _ocrHost = "";
    public string OcrHost
    {
        get => _ocrHost;
        set => this.RaiseAndSetIfChanged(ref _ocrHost, value);
    }

    private string _ocrPort = "";
    public string OcrPort
    {
        get => _ocrPort;
        set => this.RaiseAndSetIfChanged(ref _ocrPort, value);
    }

    private string _onnxModelPath = "";
    public string OnnxModelPath
    {
        get => _onnxModelPath;
        set => this.RaiseAndSetIfChanged(ref _onnxModelPath, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseOnnxPathCommand { get; }

    private int _ocrRetryCount = 3;
    public int OcrRetryCount
    {
        get => _ocrRetryCount;
        set => this.RaiseAndSetIfChanged(ref _ocrRetryCount, value);
    }

    // Sync Settings
    private int _maxSyncPages = 100;
    public int MaxSyncPages
    {
        get => _maxSyncPages;
        set => this.RaiseAndSetIfChanged(ref _maxSyncPages, value);
    }

    private int _earlyStopThreshold = 5;
    public int EarlyStopThreshold
    {
        get => _earlyStopThreshold;
        set => this.RaiseAndSetIfChanged(ref _earlyStopThreshold, value);
    }

    private bool _autoMergeAfterSync = true;
    public bool AutoMergeAfterSync
    {
        get => _autoMergeAfterSync;
        set => this.RaiseAndSetIfChanged(ref _autoMergeAfterSync, value);
    }

    // Data Settings
    private string _dataDirectory = "Data";
    public string DataDirectory
    {
        get => _dataDirectory;
        set => this.RaiseAndSetIfChanged(ref _dataDirectory, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseDataDirectoryCommand { get; }

    private int _snapshotKeepCount = 10;
    public int SnapshotKeepCount
    {
        get => _snapshotKeepCount;
        set => this.RaiseAndSetIfChanged(ref _snapshotKeepCount, value);
    }

    // UI Settings
    public ObservableCollection<string> Themes { get; } = ["浅色", "深色", "跟随系统"];

    private string _selectedTheme = "浅色";
    public string SelectedTheme
    {
        get => _selectedTheme;
        set => this.RaiseAndSetIfChanged(ref _selectedTheme, value);
    }

    private bool _rememberDefaultIdentity;
    public bool RememberDefaultIdentity
    {
        get => _rememberDefaultIdentity;
        set => this.RaiseAndSetIfChanged(ref _rememberDefaultIdentity, value);
    }

    public ObservableCollection<string> IdentityList { get; } = [];

    private string _selectedDefaultIdentity = "";
    public string SelectedDefaultIdentity
    {
        get => _selectedDefaultIdentity;
        set => this.RaiseAndSetIfChanged(ref _selectedDefaultIdentity, value);
    }

    // Classification Settings
    private string _rulesPath = "";
    public string RulesPath
    {
        get => _rulesPath;
        set => this.RaiseAndSetIfChanged(ref _rulesPath, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseRulesPathCommand { get; }

    private string _rulesUpdateUrl = "";
    public string RulesUpdateUrl
    {
        get => _rulesUpdateUrl;
        set => this.RaiseAndSetIfChanged(ref _rulesUpdateUrl, value);
    }

    public ReactiveCommand<Unit, Unit> UpdateRulesCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetRulesCommand { get; }

    // Update Settings
    private bool _autoCheckUpdate = true;
    public bool AutoCheckUpdate
    {
        get => _autoCheckUpdate;
        set => this.RaiseAndSetIfChanged(ref _autoCheckUpdate, value);
    }

    private int _checkIntervalHours = 24;
    public int CheckIntervalHours
    {
        get => _checkIntervalHours;
        set => this.RaiseAndSetIfChanged(ref _checkIntervalHours, value);
    }

    public ReactiveCommand<Unit, Unit> CheckUpdateNowCommand { get; }

    // Action commands
    public ReactiveCommand<Unit, Unit> ResetDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public SettingsViewModel()
    {
        var nav = NavigationService.Instance;

        // Load current config
        LoadFromConfig();

        // Load identity list
        LoadIdentityList();

        // Commands
        SetPasswordCommand = ReactiveCommand.Create(() =>
        {
            if (!string.IsNullOrEmpty(ProtectionPassword))
            {
                EnableStartupProtection = true;
            }
        });

        BrowseOnnxPathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await nav.ShowOpenFileDialogAsync("选择ONNX模型文件", ["onnx"]);
            if (result.Length > 0)
                OnnxModelPath = result[0];
        });

        BrowseDataDirectoryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await nav.ShowOpenFolderDialogAsync("选择数据目录");
            if (result != null)
                DataDirectory = result;
        });

        BrowseRulesPathCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var result = await nav.ShowOpenFileDialogAsync("选择规则文件", ["toml"]);
            if (result.Length > 0)
                RulesPath = result[0];
        });

        UpdateRulesCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (string.IsNullOrEmpty(RulesUpdateUrl)) return;
            try
            {
                var success = await TomlConfigService.UpdateClassificationRulesFromRemote(RulesUpdateUrl);
                ErrorMessage = success ? null : "更新规则失败";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"更新规则失败: {ex.Message}";
            }
        });

        ResetRulesCommand = ReactiveCommand.Create(() =>
        {
            RulesPath = "";
            RulesUpdateUrl = "";
        });

        CheckUpdateNowCommand = ReactiveCommand.Create(() =>
        {
            // TODO: Implement update check
        });

        ResetDefaultsCommand = ReactiveCommand.Create(() =>
        {
            LoadFromDefaults();
        });

        CancelCommand = ReactiveCommand.Create(() =>
        {
            // Reload config, discard changes
            LoadFromConfig();
        });

        SaveCommand = ReactiveCommand.Create(SaveSettings);
    }

    private void LoadFromConfig()
    {
        var config = TomlConfigService.LoadAppConfig();

        // Security
        EnableStartupProtection = config.Security.EnableStartupProtection;

        // Captcha
        SelectedCaptchaMode = config.Captcha.Mode switch
        {
            "remote_ocr" => "远程OCR",
            "local_onnx" => "本地ONNX",
            _ => "手动输入",
        };
        OcrHost = config.Captcha.RemoteOcrHost;
        OcrPort = config.Captcha.RemoteOcrPort > 0 ? config.Captcha.RemoteOcrPort.ToString() : "";
        OnnxModelPath = config.Captcha.OnnxModelPath;
        OcrRetryCount = config.Captcha.OcrRetryCount;

        // Sync
        MaxSyncPages = config.Sync.MaxPages;
        EarlyStopThreshold = config.Sync.EarlyStopThreshold;
        AutoMergeAfterSync = config.Sync.AutoMergeAfterSync;

        // Data
        DataDirectory = config.Data.DataDirectory;
        SnapshotKeepCount = config.Data.SnapshotKeepCount;

        // UI
        SelectedTheme = config.Ui.Theme switch
        {
            "dark" => "深色",
            "system" => "跟随系统",
            _ => "浅色",
        };
        RememberDefaultIdentity = config.Identity.RememberDefault;

        // Classification
        RulesPath = config.Classification.RulesPath;
        RulesUpdateUrl = config.Classification.RulesUpdateUrl;

        // Update
        AutoCheckUpdate = config.Update.AutoCheck;
        CheckIntervalHours = config.Update.CheckIntervalHours;
    }

    private void LoadFromDefaults()
    {
        var config = new AppConfig();
        EnableStartupProtection = config.Security.EnableStartupProtection;
        SelectedCaptchaMode = "手动输入";
        OcrHost = "";
        OcrPort = "";
        OnnxModelPath = "";
        OcrRetryCount = 3;
        MaxSyncPages = 100;
        EarlyStopThreshold = 5;
        AutoMergeAfterSync = true;
        DataDirectory = "Data";
        SnapshotKeepCount = 10;
        SelectedTheme = "浅色";
        RememberDefaultIdentity = false;
        RulesPath = "";
        RulesUpdateUrl = "";
        AutoCheckUpdate = true;
        CheckIntervalHours = 24;
    }

    private void SaveSettings()
    {
        TomlConfigService.UpdateAppConfig(config =>
        {
            // Security
            config.Security.EnableStartupProtection = EnableStartupProtection;
            if (!string.IsNullOrEmpty(ProtectionPassword))
            {
                config.Security.PasswordHash = Services.Security.EncryptionService.HashPassword(ProtectionPassword);
            }

            // Captcha
            config.Captcha.Mode = SelectedCaptchaMode switch
            {
                "远程OCR" => "remote_ocr",
                "本地ONNX" => "local_onnx",
                _ => "manual",
            };
            config.Captcha.RemoteOcrHost = OcrHost;
            config.Captcha.RemoteOcrPort = int.TryParse(OcrPort, out var port) ? port : 0;
            config.Captcha.OnnxModelPath = OnnxModelPath;
            config.Captcha.OcrRetryCount = OcrRetryCount;

            // Sync
            config.Sync.MaxPages = MaxSyncPages;
            config.Sync.EarlyStopThreshold = EarlyStopThreshold;
            config.Sync.AutoMergeAfterSync = AutoMergeAfterSync;

            // Data
            config.Data.DataDirectory = DataDirectory;
            config.Data.SnapshotKeepCount = SnapshotKeepCount;

            // UI
            config.Ui.Theme = SelectedTheme switch
            {
                "深色" => "dark",
                "跟随系统" => "system",
                _ => "light",
            };
            config.Identity.RememberDefault = RememberDefaultIdentity;

            // Classification
            config.Classification.RulesPath = RulesPath;
            config.Classification.RulesUpdateUrl = RulesUpdateUrl;

            // Update
            config.Update.AutoCheck = AutoCheckUpdate;
            config.Update.CheckIntervalHours = CheckIntervalHours;
        });
    }

    private void LoadIdentityList()
    {
        IdentityList.Clear();
        var identities = IdentityDb.GetAll();
        foreach (var identity in identities)
        {
            IdentityList.Add(identity.Name);
        }
    }
}
