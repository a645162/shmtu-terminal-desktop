using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Config;

namespace shmtu.terminal.desktop.ViewModels.Component.CountDown;

/// <summary>
/// ViewModel for the semester countdown component
/// XAML bindings: ConfigureCommand, CurrentWeek, DaysUntilHoliday, SemesterProgress
/// </summary>
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

    public ReactiveCommand<Unit, Unit> ConfigureCommand { get; }

    /// <summary>
    /// Event raised when configure is requested (open settings)
    /// </summary>
    public event Action? ConfigureRequested;

    public CountDownWeekViewModel()
    {
        ConfigureCommand = ReactiveCommand.Create(() =>
        {
            ConfigureRequested?.Invoke();
        });

        UpdateCountdown();
    }

    /// <summary>
    /// Update countdown based on current date and semester schedule
    /// Default: 2025 spring semester (Feb 17 - Jun 27, ~19 weeks)
    /// In production, this would read from AppConfig
    /// </summary>
    public void UpdateCountdown()
    {
        // Default semester dates — can be configured via settings
        var semesterStart = new DateTime(2025, 2, 17);
        var semesterEnd = new DateTime(2025, 6, 27);
        var now = DateTime.Now;

        var totalDays = (semesterEnd - semesterStart).Days;
        var elapsedDays = (now - semesterStart).Days;

        if (elapsedDays < 0)
        {
            // Semester hasn't started yet
            CurrentWeek = 0;
            DaysUntilHoliday = totalDays;
            SemesterProgress = 0;
        }
        else if (elapsedDays > totalDays)
        {
            // Semester is over
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
