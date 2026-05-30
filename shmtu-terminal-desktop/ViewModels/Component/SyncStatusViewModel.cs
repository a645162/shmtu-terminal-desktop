using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Media;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Sync;

namespace shmtu.terminal.desktop.ViewModels.Component;

public class SyncStatusViewModel : ViewModelBase
{
    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    private string _statusText = "就绪";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    private IBrush _statusColor = new SolidColorBrush(Color.Parse("#5E6A79"));
    public IBrush StatusColor
    {
        get => _statusColor;
        set => this.RaiseAndSetIfChanged(ref _statusColor, value);
    }

    private string _accountProgress = "";
    public string AccountProgress
    {
        get => _accountProgress;
        set => this.RaiseAndSetIfChanged(ref _accountProgress, value);
    }

    private string _currentAccount = "";
    public string CurrentAccount
    {
        get => _currentAccount;
        set => this.RaiseAndSetIfChanged(ref _currentAccount, value);
    }

    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    private string _progressText = "";
    public string ProgressText
    {
        get => _progressText;
        set => this.RaiseAndSetIfChanged(ref _progressText, value);
    }

    private string _detailText = "";
    public string DetailText
    {
        get => _detailText;
        set => this.RaiseAndSetIfChanged(ref _detailText, value);
    }

    public ObservableCollection<string> StatusLog { get; } = [];

    public ReactiveCommand<Unit, Unit> DismissCommand { get; }

    private CancellationTokenSource? _autoHideCts;

    /// <summary>
    /// Account tracking state: current index and total count across the sync session
    /// </summary>
    private int _currentAccountIndex;
    private int _totalAccountCount;

    public SyncStatusViewModel()
    {
        DismissCommand = ReactiveCommand.Create(OnDismiss);
    }

    /// <summary>
    /// Initialize for a new sync session with the given account count
    /// </summary>
    public void StartSync(int totalAccounts)
    {
        _currentAccountIndex = 0;
        _totalAccountCount = totalAccounts;
        IsVisible = true;
        IsRunning = true;
        StatusLog.Clear();
        ProgressValue = 0;
        ProgressText = "";
        AccountProgress = totalAccounts > 0 ? $"1/{totalAccounts}" : "";
        CurrentAccount = "";
        DetailText = "";
        StatusText = "同步中";
        StatusColor = new SolidColorBrush(Color.Parse("#0F6CBD"));
        CancelAutoHide();
    }

    /// <summary>
    /// Update progress from BillSyncService's ProgressChanged event
    /// </summary>
    public void UpdateProgress(SyncProgress progress)
    {
        CurrentAccount = progress.AccountName;

        switch (progress.Status)
        {
            case "syncing":
                IsRunning = true;
                StatusText = "同步中";
                StatusColor = new SolidColorBrush(Color.Parse("#0F6CBD"));

                if (progress.TotalPages > 0)
                {
                    ProgressValue = (double)progress.CurrentPage / progress.TotalPages * 100;
                    ProgressText = $"第 {progress.CurrentPage}/{progress.TotalPages} 页";
                    AddLog($"正在从校园平台拉取账单第 {progress.CurrentPage}/{progress.TotalPages} 页... (+{progress.NewCount}条)");
                }
                else
                {
                    AddLog($"正在同步: {progress.AccountName}...");
                }
                break;

            case "completed":
                // An account just finished — advance the index
                _currentAccountIndex++;
                AccountProgress = _totalAccountCount > 0
                    ? $"{_currentAccountIndex}/{_totalAccountCount}"
                    : "";

                if (progress.NewCount > 0)
                    AddLog($"{progress.AccountName} 同步完成 (+{progress.NewCount}条)");
                else
                    AddLog($"{progress.AccountName} 同步完成（无新数据）");

                // If there are more accounts, stay in running state
                if (_currentAccountIndex < _totalAccountCount)
                {
                    IsRunning = true;
                    StatusText = "同步中";
                    StatusColor = new SolidColorBrush(Color.Parse("#0F6CBD"));
                }
                break;

            case "failed":
                _currentAccountIndex++;
                AccountProgress = _totalAccountCount > 0
                    ? $"{_currentAccountIndex}/{_totalAccountCount}"
                    : "";

                var errorMsg = !string.IsNullOrEmpty(progress.ErrorMessage)
                    ? progress.ErrorMessage
                    : "未知错误";
                AddLog($"{progress.AccountName} 同步失败: {errorMsg}");

                if (_currentAccountIndex < _totalAccountCount)
                {
                    IsRunning = true;
                    StatusText = "同步中";
                    StatusColor = new SolidColorBrush(Color.Parse("#0F6CBD"));
                }
                break;

            case "captcha_required":
                IsRunning = true;
                StatusText = "需要验证码";
                StatusColor = new SolidColorBrush(Color.Parse("#F6BD16"));
                AddLog($"{progress.AccountName} 需要验证码输入");
                break;
        }
    }

    /// <summary>
    /// Show completed state after all accounts are done
    /// </summary>
    public void ShowCompleted(int newCount)
    {
        IsRunning = false;
        StatusText = "已完成";
        StatusColor = new SolidColorBrush(Color.Parse("#1A8F5A"));
        ProgressValue = 100;
        ProgressText = newCount > 0 ? $"新增 {newCount} 条记录" : "无新数据";
        AddLog($"同步完成，共新增 {newCount} 条记录");
        AutoHide();
    }

    /// <summary>
    /// Show error state
    /// </summary>
    public void ShowError(string error)
    {
        IsRunning = false;
        StatusText = "同步失败";
        StatusColor = new SolidColorBrush(Color.Parse("#E86452"));
        ProgressText = "";
        AddLog($"同步失败: {error}");
        AutoHide();
    }

    /// <summary>
    /// Auto-hide the panel after 3 seconds
    /// </summary>
    public void AutoHide()
    {
        CancelAutoHide();
        _autoHideCts = new CancellationTokenSource();
        var token = _autoHideCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000, token);
                if (!token.IsCancellationRequested)
                {
                    IsVisible = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled — don't hide
            }
        }, token);
    }

    private void CancelAutoHide()
    {
        _autoHideCts?.Cancel();
        _autoHideCts?.Dispose();
        _autoHideCts = null;
    }

    private void OnDismiss()
    {
        if (IsRunning)
        {
            // Running state — cancel is handled by the caller via SyncCancelled event
            // The MainWindowViewModel listens to this
            SyncCancelled?.Invoke();
        }

        CancelAutoHide();
        IsVisible = false;
    }

    /// <summary>
    /// Raised when the user clicks Cancel during a running sync
    /// </summary>
    public event Action? SyncCancelled;

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";
        StatusLog.Insert(0, entry);

        // Keep the log from growing unbounded
        while (StatusLog.Count > 50)
            StatusLog.RemoveAt(StatusLog.Count - 1);

        DetailText = entry;
    }
}
