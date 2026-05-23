using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.ViewModels.MainWindowTab;

namespace shmtu.terminal.desktop.ViewModels;

/// <summary>
/// ViewModel for the main window
/// XAML bindings: CurrentIdentityName, SyncStatus, LastSyncTime, SelectedTabIndex,
///   HomeViewModel, BillViewModel, FeaturesViewModel
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private string _currentIdentityName = "";
    public string CurrentIdentityName
    {
        get => _currentIdentityName;
        set => this.RaiseAndSetIfChanged(ref _currentIdentityName, value);
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

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public HomeTabViewModel HomeViewModel { get; }
    public BillTabViewModel BillViewModel { get; }
    public FeaturesTabViewModel FeaturesViewModel { get; }

    private int _currentIdentityId;

    /// <summary>
    /// The currently selected identity ID
    /// </summary>
    public int CurrentIdentityId
    {
        get => _currentIdentityId;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentIdentityId, value);
            OnIdentityChanged(value);
        }
    }

    public MainWindowViewModel()
    {
        HomeViewModel = new HomeTabViewModel();
        BillViewModel = new BillTabViewModel();
        FeaturesViewModel = new FeaturesTabViewModel();

        // Subscribe to sync requests from bill tab
        BillViewModel.SyncRequested += OnSyncRequested;
    }

    /// <summary>
    /// Initialize with a specific identity
    /// </summary>
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
        }

        // Propagate identity to child ViewModels
        HomeViewModel.IdentityId = identityId;
        BillViewModel.IdentityId = identityId;
    }

    private async void OnSyncRequested()
    {
        try
        {
            SyncStatus = "同步中...";

            // Use BillSyncService to sync all accounts under the current identity
            var syncService = new Services.Sync.BillSyncService();

            // Subscribe to progress
            syncService.ProgressChanged += progress =>
            {
                SyncStatus = progress.Status switch
                {
                    "syncing" => $"正在同步: {progress.AccountName}...",
                    "completed" => $"已完成: {progress.AccountName} (+{progress.NewCount}条)",
                    "failed" => $"失败: {progress.AccountName}",
                    _ => progress.Status,
                };
            };

            // For now, use manual captcha resolver
            // In production, this would use the configured resolver from AppConfig
            var captchaResolver = new shmtu.cas.captcha.ManualCaptchaResolver(async (img, _) =>
            {
                // Manual resolution - would show a dialog to user
                // For now, return empty to skip this account
                throw new NotSupportedException("Manual captcha resolution requires user interaction. Configure OCR instead.");
            });

            var result = await syncService.SyncIdentityAsync(
                _currentIdentityId, captchaResolver);

            SyncStatus = $"同步完成: {result.SuccessCount}成功, {result.FailedCount}失败, +{result.TotalNewBills}条";
            LastSyncTime = $"最后同步: {DateTime.Now:HH:mm:ss}";

            // Refresh data after sync
            HomeViewModel.RefreshData();
            BillViewModel.RefreshData();
        }
        catch (Exception ex)
        {
            SyncStatus = $"同步失败: {ex.Message}";
        }
    }
}
