using System.Reactive;
using ReactiveUI;
using System.Reactive;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;

namespace shmtu.terminal.desktop.ViewModels.Component.CountDown;

public class CountDownWeekViewModel : ViewModelBase
{
    private int _currentWeek;
    public int CurrentWeek
    {
        get => _currentWeek;
        set => this.RaiseAndSetIfChanged(ref _currentWeek, value);
    }

    private int _daysUntilHoliday;
    public int DaysUntilHoliday
    {
        get => _daysUntilHoliday;
        set => this.RaiseAndSetIfChanged(ref _daysUntilHoliday, value);
    }

    private double _semesterProgress;
    public double SemesterProgress
    {
        get => _semesterProgress;
        set => this.RaiseAndSetIfChanged(ref _semesterProgress, value);
    }

    // HIGH 13: 学期日期从 AppConfig 读取
    private DateTime _semesterStart;
    public DateTime SemesterStart
    {
        get => _semesterStart;
        set => this.RaiseAndSetIfChanged(ref _semesterStart, value);
    }

    private DateTime _semesterEnd;
    public DateTime SemesterEnd
    {
        get => _semesterEnd;
        set => this.RaiseAndSetIfChanged(ref _semesterEnd, value);
    }

    public ReactiveCommand<Unit, Unit> ConfigureCommand { get; }

    /// <summary>
    /// HIGH 14: 订阅 ConfigureRequested 事件，打开设置页面
    /// </summary>
    public event Action? ConfigureRequested
    {
        add
        {
            _configureRequested += value;
            _subscribeToNav();
        }
        remove => _configureRequested -= value;
    }

    private event Action? _configureRequested;
    private bool _subscribed;

    public CountDownWeekViewModel()
    {
        ConfigureCommand = ReactiveCommand.Create(() =>
        {
            _configureRequested?.Invoke();
        });

        LoadFromConfig();
        UpdateCountdown();
    }

    private void LoadFromConfig()
    {
        // HIGH 13: 从 AppConfig 读取学期日期，无配置则用默认值
        var now = DateTime.Now;
        var year = now.Month >= 2 && now.Month <= 6 ? now.Year : now.Year;

        var config = TomlConfigService.LoadAppConfig();
        var sem = config.Semester;
        if (sem?.StartDate != default)
            SemesterStart = sem.StartDate;
        else
            SemesterStart = new DateTime(year, 2, 17);  // 春季学期开始

        if (sem?.EndDate != default)
            SemesterEnd = sem.EndDate;
        else
            SemesterEnd = new DateTime(year, 6, 27);   // 春季学期结束
    }

    private void _subscribeToNav()
    {
        if (_subscribed) return;
        _subscribed = true;
    }

    public void UpdateCountdown()
    {
        var now = DateTime.Now;
        var totalDays = (SemesterEnd - SemesterStart).Days;
        var elapsedDays = (now - SemesterStart).Days;

        if (elapsedDays < 0)
        {
            CurrentWeek = 0;
            DaysUntilHoliday = totalDays;
            SemesterProgress = 0;
        }
        else if (elapsedDays > totalDays)
        {
            CurrentWeek = totalDays / 7 + 1;
            DaysUntilHoliday = 0;
            SemesterProgress = 100;
        }
        else
        {
            CurrentWeek = elapsedDays / 7 + 1;
            DaysUntilHoliday = totalDays - elapsedDays;
            SemesterProgress = Math.Round((double)elapsedDays / totalDays * 100, 1);
        }
    }
}
