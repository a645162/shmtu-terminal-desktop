using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.Config;

namespace shmtu.terminal.desktop.ViewModels.Component.Bill;

public class BillFilterViewModel : ViewModelBase
{
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public ObservableCollection<string> BillTypes { get; } =
        ["全部", "充值", "消费", "电费", "洗澡", "热水", "食堂", "蛋糕", "其他"];

    private string _selectedBillType = "全部";
    public string SelectedBillType
    {
        get => _selectedBillType;
        set => this.RaiseAndSetIfChanged(ref _selectedBillType, value);
    }

    public ObservableCollection<string> TimeRanges { get; } =
        ["全部", "今天", "近7天", "近30天", "本月", "自定义"];

    private string _selectedTimeRange = "全部";
    public string SelectedTimeRange
    {
        get => _selectedTimeRange;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTimeRange, value);
            this.RaisePropertyChanged(nameof(IsCustomTimeRange));
        }
    }

    public bool IsCustomTimeRange => SelectedTimeRange == "自定义";

    private DateTime? _startDate = DateTime.Today.AddMonths(-1);
    public DateTime? StartDate
    {
        get => _startDate;
        set => this.RaiseAndSetIfChanged(ref _startDate, value);
    }

    private DateTime? _endDate = DateTime.Today;
    public DateTime? EndDate
    {
        get => _endDate;
        set => this.RaiseAndSetIfChanged(ref _endDate, value);
    }

    public event Action? FilterChanged;
    public event Action? SyncRequested;

    public ReactiveCommand<Unit, Unit> SyncCommand { get; }

    private bool _initialized = false;

    public BillFilterViewModel()
    {
        SyncCommand = ReactiveCommand.Create(() => SyncRequested?.Invoke());

        // CRITICAL 8: SearchText 防抖 — 300ms 内只触发一次
        this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => FilterChanged?.Invoke());

        // 其他过滤器：跳过首次订阅，避免 ViewModel 初始化时触发
        this.WhenAnyValue(x => x.SelectedBillType, x => x.SelectedTimeRange,
            x => x.StartDate, x => x.EndDate, x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.TaskpoolScheduler)
            .Subscribe(_ => {
                if (_initialized)
                    FilterChanged?.Invoke();
                _initialized = true;
            });
    }

    public (long? StartTimestamp, long? EndTimestamp) GetTimeRange()
    {
        long? startTs = null;
        long? endTs = null;
        var now = DateTime.Now;
        var today = now.Date;

        switch (SelectedTimeRange)
        {
            case "今天":
                startTs = ((DateTimeOffset)today).ToUnixTimeSeconds();
                endTs = ((DateTimeOffset)today.AddDays(1)).ToUnixTimeSeconds();
                break;
            case "近7天":
                startTs = ((DateTimeOffset)today.AddDays(-7)).ToUnixTimeSeconds();
                endTs = ((DateTimeOffset)today.AddDays(1)).ToUnixTimeSeconds();
                break;
            case "近30天":
                startTs = ((DateTimeOffset)today.AddDays(-30)).ToUnixTimeSeconds();
                endTs = ((DateTimeOffset)today.AddDays(1)).ToUnixTimeSeconds();
                break;
            case "本月":
                startTs = ((DateTimeOffset)new DateTime(now.Year, now.Month, 1)).ToUnixTimeSeconds();
                endTs = ((DateTimeOffset)today.AddDays(1)).ToUnixTimeSeconds();
                break;
            case "自定义":
                if (StartDate.HasValue)
                    startTs = ((DateTimeOffset)StartDate.Value).ToUnixTimeSeconds();
                if (EndDate.HasValue)
                    endTs = ((DateTimeOffset)EndDate.Value.AddDays(1)).ToUnixTimeSeconds();
                break;
        }
        return (startTs, endTs);
    }
}
