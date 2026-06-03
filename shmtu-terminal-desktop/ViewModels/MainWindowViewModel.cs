using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using shmtu.cas.auth;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.Services.Sync;
using shmtu.terminal.desktop.ViewModels.Component.Captcha;
using shmtu.terminal.desktop.ViewModels.Component;
using shmtu.terminal.desktop.ViewModels.MainWindowTab;
using shmtu.terminal.desktop.ViewModels.Startup;
using shmtu.terminal.desktop.ViewModels.User;
using shmtu.terminal.desktop.Views.Component.Captcha;

namespace shmtu.terminal.desktop.ViewModels;

/// <summary>
/// ViewModel for the main window
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private string _currentIdentityName = "";
    public string CurrentIdentityName
    {
        get => _currentIdentityName;
        set => this.RaiseAndSetIfChanged(ref _currentIdentityName, value);
    }

    private string _currentIdentityShortName = "";
    public string CurrentIdentityShortName
    {
        get => _currentIdentityShortName;
        set => this.RaiseAndSetIfChanged(ref _currentIdentityShortName, value);
    }

    private string _syncStatus = "就绪";
    public string SyncStatus
    {
        get => _syncStatus;
        set => this.RaiseAndSetIfChanged(ref _syncStatus, value);
    }

    private string _lastSyncTime = "";
    public string LastSyncTime
    {
        get => _lastSyncTime;
        set => this.RaiseAndSetIfChanged(ref _lastSyncTime, value);
    }

    private string _themeIcon = ""; // WeatherSunny
    public string ThemeIcon
    {
        get => _themeIcon;
        set => this.RaiseAndSetIfChanged(ref _themeIcon, value);
    }

    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => this.RaiseAndSetIfChanged(ref _isDarkTheme, value);
    }

    public string AppVersion { get; } = GetAppVersion();

    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public HomeTabViewModel HomeViewModel { get; }
    public BillTabViewModel BillViewModel { get; }
    public FeaturesTabViewModel FeaturesViewModel { get; }
    public SyncStatusViewModel SyncStatusVm { get; }

    private int _currentIdentityId;

    public int CurrentIdentityId
    {
        get => _currentIdentityId;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentIdentityId, value);
            OnIdentityChanged(value);
        }
    }

    public ObservableCollection<IdentityInfo> AvailableIdentities { get; } = [];

    public ReactiveCommand<Unit, Unit> SwitchIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> ManageIdentityCommand { get; }

    public MainWindowViewModel()
    {
        HomeViewModel = new HomeTabViewModel();
        BillViewModel = new BillTabViewModel();
        FeaturesViewModel = new FeaturesTabViewModel();
        SyncStatusVm = new SyncStatusViewModel();

        BillViewModel.SyncRequested += OnSyncRequested;

        SwitchIdentityCommand = ReactiveCommand.Create(OpenIdentitySelect);
        ManageIdentityCommand = ReactiveCommand.Create(() =>
        {
            NavigationService.Instance.OpenWindow("IdentityManager", new IdentityManagerViewModel());
        });
        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme);

        LoadAvailableIdentities();
    }

    private void LoadAvailableIdentities()
    {
        AvailableIdentities.Clear();
        foreach (var identity in IdentityDb.GetEnabled())
        {
            AvailableIdentities.Add(identity);
        }
    }

    private void OpenIdentitySelect()
    {
        var selectViewModel = new IdentitySelectViewModel();
        selectViewModel.IdentitySelected += identityId =>
        {
            CurrentIdentityId = identityId;
            LoadAvailableIdentities();
        };
        NavigationService.Instance.OpenWindow("IdentitySelect", selectViewModel);
    }

    public void InitializeWithIdentity(int identityId)
    {
        CurrentIdentityId = identityId;
    }

    private void OnIdentityChanged(int identityId)
    {
        if (identityId <= 0) return;

        var identity = IdentityDb.GetById(identityId);
        if (identity != null)
        {
            CurrentIdentityName = $"当前身份: {identity.Name}";
            CurrentIdentityShortName = identity.Name;
        }

        HomeViewModel.IdentityId = identityId;
        BillViewModel.IdentityId = identityId;
    }

    /// <summary>
    /// 根据 AppConfig.Captcha.Mode 选择对应的验证码解析器
    /// </summary>
    private ICaptchaResolver CreateCaptchaResolver()
    {
        var config = TomlConfigService.LoadAppConfig();

        return config.Captcha.Mode switch
        {
            "remote_ocr" => new RemoteOcrCaptchaResolver(
                config.Captcha.RemoteOcrHost,
                config.Captcha.RemoteOcrPort > 0 ? config.Captcha.RemoteOcrPort : 21601),

            "remote_ocr_http" => new RemoteOcrHttpCaptchaResolver(
                config.Captcha.RemoteOcrHttpUrl,
                config.Captcha.OcrRetryCount > 0 ? config.Captcha.OcrRetryCount : 3),

            "local_onnx" => new ManualCaptchaResolver(async (imageData, ct) =>
                await ShowManualCaptchaDialogAsync(imageData, ct)),

            _ => new ManualCaptchaResolver(async (imageData, ct) =>
            {
                return await ShowManualCaptchaDialogAsync(imageData, ct);
            }),
        };
    }

    /// <summary>
    /// 弹出 Avalonia 窗口让用户输入验证码
    /// 当用户点击"刷新验证码"时，关闭对话框并返回空值，
    /// 由外层重试循环重新获取验证码图片
    /// </summary>
    private async Task<CaptchaAnswer> ShowManualCaptchaDialogAsync(
        byte[] imageData,
        CancellationToken cancellationToken)
    {
        var vm = new ManualCaptchaViewModel();
        vm.SetImageBytes(imageData);
        vm.Tcs = new TaskCompletionSource<CaptchaResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var window = new ManualCaptchaWindow { DataContext = vm };
        var mainWin = GetMainWindow();
        if (mainWin == null)
            throw new InvalidOperationException("无法获取主窗口");

        void OnCloseRequested()
        {
            if (window.IsVisible)
                window.Close();
        }

        void OnClosed(object? sender, EventArgs args)
        {
            vm.Tcs?.TrySetResult(new CaptchaResult
            {
                Expression = vm.CaptchaExpression,
                Answer = vm.CaptchaAnswer.Trim(),
                Success = !string.IsNullOrWhiteSpace(vm.CaptchaAnswer),
            });
        }

        void OnRefreshRequested()
        {
            // Close the dialog so the caller's retry loop will re-fetch the captcha
            if (window.IsVisible)
                window.Close();
        }

        vm.CloseRequested += OnCloseRequested;
        vm.RefreshRequested += OnRefreshRequested;
        window.Closed += OnClosed;

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            vm.Tcs?.TrySetCanceled(cancellationToken);
            if (window.IsVisible)
                window.Close();
        });

        var dialogTask = window.ShowDialog(mainWin);
        try
        {
            var result = await vm.Tcs!.Task.WaitAsync(cancellationToken);
            if (window.IsVisible)
                window.Close();

            await dialogTask;

            // If user requested a refresh, return empty answer so the retry loop
            // in SubmitLoginAsync will fetch a new captcha image
            if (result.IsRefresh)
                return new CaptchaAnswer("", CaptchaAnswerKind.Answer);

            return new CaptchaAnswer(result.Answer, CaptchaAnswerKind.Answer);
        }
        finally
        {
            vm.CloseRequested -= OnCloseRequested;
            vm.RefreshRequested -= OnRefreshRequested;
            window.Closed -= OnClosed;
        }
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return TopLevel.GetTopLevel(desktop.MainWindow);
        return null;
    }

    /// <summary>
    /// 参考 BillDemo.LoginWithRetry：登录失败自动重试，最多5次
    /// </summary>
    private async Task<bool> LoginWithRetryAsync(
        EpayAuth epayAuth,
        string username,
        string password,
        ICaptchaResolver resolver,
        CancellationToken ct)
    {
        const int maxAttempts = 5;

        var probe = await epayAuth.ProbeLoginAsync(ct);
        if (probe is LoginProbe.AlreadyLoggedIn)
            return true;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            SyncStatus = $"第{attempt}/{maxAttempts}次登录尝试...";

            var result = await epayAuth.SubmitLoginAsync(username, password, ct);
            switch (result)
            {
                case LoginSubmitResult.Success:
                    if (await epayAuth.TestLoginStatusAsync(ct))
                    {
                        SyncStatus = "登录验证成功！";
                        return true;
                    }
                    SyncStatus = "登录验证失败";
                    return false;

                case LoginSubmitResult.ValidateCodeError:
                    SyncStatus = "验证码错误，重试中...";
                    continue;

                case LoginSubmitResult.PasswordError:
                    SyncStatus = "用户名或密码错误";
                    return false;

                case LoginSubmitResult.Failure f:
                    SyncStatus = $"登录失败: {f.Message}";
                    return false;
            }
        }

        SyncStatus = $"超过最大重试次数 {maxAttempts}";
        return false;
    }

    /// <summary>
    /// 处理同步请求 — 入口
    /// </summary>
    private async Task OnSyncRequestedAsync(CancellationToken ct)
    {
        if (_currentIdentityId <= 0)
        {
            SyncStatus = "请先选择身份";
            return;
        }

        SyncStatus = "同步中...";

        var syncService = new BillSyncService();

        // Count enabled accounts for the floating panel
        var accounts = AccountDb.GetEnabledByIdentityId(_currentIdentityId);
        SyncStatusVm.StartSync(accounts.Count);

        syncService.ProgressChanged += progress =>
        {
            SyncStatusVm.UpdateProgress(progress);

            // Also update the legacy SyncStatus for the status bar
            SyncStatus = progress.Status switch
            {
                "syncing" when progress.TotalPages > 0 =>
                    $"正在同步: {progress.AccountName} 第{progress.CurrentPage}/{progress.TotalPages}页 (+{progress.NewCount}条)",
                "syncing" =>
                    $"正在同步: {progress.AccountName}...",
                "captcha_error" => $"验证码错误，正在重试: {progress.AccountName}",
                "completed" => $"已完成: {progress.AccountName} (+{progress.NewCount}条)",
                "failed" => $"失败: {progress.AccountName}",
                _ => progress.Status,
            };
        };

        var config = TomlConfigService.LoadAppConfig();
        var captchaResolver = CreateCaptchaResolver();

        try
        {
            var result = await syncService.SyncIdentityAsync(_currentIdentityId, captchaResolver, ct);

            SyncStatus = $"同步完成: {result.SuccessCount}成功, {result.FailedCount}失败, +{result.TotalNewBills}条";
            LastSyncTime = $"最后同步: {DateTime.Now:HH:mm:ss}";

            SyncStatusVm.ShowCompleted(result.TotalNewBills);

            HomeViewModel.RefreshData();
            BillViewModel.RefreshData();
        }
        catch (Exception ex)
        {
            SyncStatus = $"同步失败: {ex.Message}";
            ErrorMessage = $"同步错误: {ex.Message}";
            SyncStatusVm.ShowError(ex.Message);
        }
        finally
        {
            if (captchaResolver is IDisposable d)
                d.Dispose();
        }
    }

    /// <summary>
    /// 同步请求事件处理（兼容原事件签名）
    /// </summary>
    private void OnSyncRequested()
    {
        _ = OnSyncRequestedAsync(CancellationToken.None);
    }

    /// <summary>
    /// Toggle between light and dark theme
    /// </summary>
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = IsDarkTheme
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
        }

        // MDL2 icons:  = WeatherMoon,  = WeatherSunny
        ThemeIcon = IsDarkTheme ? "" : "";

        // Persist theme to config
        try
        {
            TomlConfigService.UpdateAppConfig(config =>
            {
                config.Ui.Theme = IsDarkTheme ? "dark" : "light";
            });
        }
        catch
        {
            // Ignore config save errors
        }
    }

    private static string GetAppVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.1.0";
        }
        catch
        {
            return "0.1.0";
        }
    }
}
