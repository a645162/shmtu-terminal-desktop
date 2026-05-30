using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Services.Statistics;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace shmtu.terminal.desktop.ViewModels.Program;

/// <summary>
/// ViewModel for the Statistics window
/// Matches Rust's StatisticsDialog with overview/category/position/compare tabs
/// Uses LiveCharts2 for all chart rendering
/// </summary>
public class StatisticsViewModel : ViewModelBase
{
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
            this.RaisePropertyChanged(nameof(IsOverviewTab));
            this.RaisePropertyChanged(nameof(IsCategoryTab));
            this.RaisePropertyChanged(nameof(IsPositionTab));
            this.RaisePropertyChanged(nameof(IsCompareTab));
        }
    }

    public bool IsOverviewTab => _selectedTabIndex == 0;
    public bool IsCategoryTab => _selectedTabIndex == 1;
    public bool IsPositionTab => _selectedTabIndex == 2;
    public bool IsCompareTab => _selectedTabIndex == 3;

    // Filter controls
    public ObservableCollection<string> IdentityList { get; } = [];
    private string _selectedIdentity = "";
    public string SelectedIdentity
    {
        get => _selectedIdentity;
        set { this.RaiseAndSetIfChanged(ref _selectedIdentity, value); RefreshAllData(); }
    }

    public ObservableCollection<string> DateRangePresets { get; } =
        ["今天", "近7天", "本周", "本月", "近30天", "本季度", "近半年", "本年"];

    private string _selectedDateRange = "本月";
    public string SelectedDateRange
    {
        get => _selectedDateRange;
        set { this.RaiseAndSetIfChanged(ref _selectedDateRange, value); RefreshAllData(); }
    }

    // Summary stats (overview tab)
    private string _totalExpense = "0.00";
    public string TotalExpense
    {
        get => _totalExpense;
        set => this.RaiseAndSetIfChanged(ref _totalExpense, value);
    }

    private string _totalIncome = "0.00";
    public string TotalIncome
    {
        get => _totalIncome;
        set => this.RaiseAndSetIfChanged(ref _totalIncome, value);
    }

    private string _netExpense = "0.00";
    public string NetExpense
    {
        get => _netExpense;
        set => this.RaiseAndSetIfChanged(ref _netExpense, value);
    }

    private string _dailyAverage = "0.00";
    public string DailyAverage
    {
        get => _dailyAverage;
        set => this.RaiseAndSetIfChanged(ref _dailyAverage, value);
    }

    private int _expenseCount;
    public int ExpenseCount
    {
        get => _expenseCount;
        set => this.RaiseAndSetIfChanged(ref _expenseCount, value);
    }

    private int _incomeCount;
    public int IncomeCount
    {
        get => _incomeCount;
        set => this.RaiseAndSetIfChanged(ref _incomeCount, value);
    }

    // LiveCharts2 Series collections

    // Daily trend - grouped column chart (expense red, income green)
    public ObservableCollection<ISeries> DailySeries { get; set; } = [];
    public ObservableCollection<Axis> DailyXAxes { get; set; } = [new Axis { Labels = [] }];
    public ObservableCollection<Axis> DailyYAxes { get; set; } = [new Axis { Labeler = v => v.ToString("F0") }];

    // Category ranking - horizontal bar chart
    public ObservableCollection<ISeries> CategorySeries { get; set; } = [];
    public ObservableCollection<Axis> CategoryXAxes { get; set; } = [new Axis { Labeler = v => v.ToString("F0") }];
    public ObservableCollection<Axis> CategoryYAxes { get; set; } = [new Axis { Labels = [] }];

    // Category pie chart
    public ObservableCollection<ISeries> CategoryPieSeries { get; set; } = [];

    // Consumption distribution - column chart
    public ObservableCollection<ISeries> ConsumptionSeries { get; set; } = [];
    public ObservableCollection<Axis> ConsumptionXAxes { get; set; } = [new Axis { Labels = [] }];
    public ObservableCollection<Axis> ConsumptionYAxes { get; set; } = [new Axis { Labeler = v => v.ToString("F0") }];

    // Meal distribution - column chart
    public ObservableCollection<ISeries> MealSeries { get; set; } = [];
    public ObservableCollection<Axis> MealXAxes { get; set; } = [new Axis { Labels = [] }];
    public ObservableCollection<Axis> MealYAxes { get; set; } = [new Axis { Labeler = v => v.ToString("F0") }];

    // Merchant ranking - horizontal bar chart
    public ObservableCollection<ISeries> MerchantSeries { get; set; } = [];
    public ObservableCollection<Axis> MerchantXAxes { get; set; } = [new Axis { Labeler = v => v.ToString("F0") }];
    public ObservableCollection<Axis> MerchantYAxes { get; set; } = [new Axis { Labels = [] }];

    // Month comparison - grouped column chart
    public ObservableCollection<ISeries> MonthCompareSeries { get; set; } = [];
    public ObservableCollection<Axis> MonthCompareXAxes { get; set; } = [new Axis { Labels = [] }];
    public ObservableCollection<Axis> MonthCompareYAxes { get; set; } = [new Axis { Labeler = v => v.ToString("F0") }];

    private int _identityId;

    // Color palette matching Rust version
    private static readonly SKColor[] ChartColors =
    [
        new(0x5B, 0x8F, 0xF9), // #5B8FF9
        new(0x5A, 0xD8, 0xA6), // #5AD8A6
        new(0xF6, 0xBD, 0x16), // #F6BD16
        new(0xE8, 0x64, 0x52), // #E86452
        new(0x6D, 0xC8, 0xEC), // #6DC8EC
        new(0x94, 0x5F, 0xB9), // #945FB9
        new(0xFF, 0x98, 0x45), // #FF9845
        new(0x1E, 0x94, 0x93), // #1E9493
        new(0xFF, 0x99, 0xC3), // #FF99C3
        new(0x26, 0x9A, 0x99), // #269A99
    ];

    private static readonly SKColor ExpenseColor = new(0xE8, 0x64, 0x52); // #E86452
    private static readonly SKColor IncomeColor = new(0x1A, 0x8F, 0x5A); // #1A8F5A

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public StatisticsViewModel()
    {
        RefreshCommand = ReactiveCommand.Create(RefreshAllData);
        LoadIdentityList();
    }

    public StatisticsViewModel(int identityId) : this()
    {
        _identityId = identityId;
        var identity = IdentityDb.GetById(identityId);
        if (identity != null)
        {
            SelectedIdentity = identity.Name;
        }
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

    private int GetIdentityIdByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return _identityId;
        var identity = IdentityDb.GetAll().FirstOrDefault(i => i.Name == name);
        return identity?.Id ?? _identityId;
    }

    private (string? Start, string? End) GetDateRange()
    {
        var now = DateTime.Now;
        var today = now.Date;
        string? start = null, end = null;

        switch (SelectedDateRange)
        {
            case "今天":
                start = today.ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "近7天":
                start = today.AddDays(-7).ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "本周":
                var dow = (int)today.DayOfWeek;
                var monday = today.AddDays(-(dow == 0 ? 6 : dow - 1));
                start = monday.ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "本月":
                start = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "近30天":
                start = today.AddDays(-30).ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "本季度":
                var qStart = new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1);
                start = qStart.ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "近半年":
                start = today.AddDays(-183).ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
            case "本年":
                start = new DateTime(now.Year, 1, 1).ToString("yyyy-MM-dd");
                end = today.ToString("yyyy-MM-dd");
                break;
        }
        return (start, end);
    }

    public void RefreshAllData()
    {
        var identityId = GetIdentityIdByName(SelectedIdentity);
        if (identityId <= 0) return;
        _identityId = identityId;

        var (dateStart, dateEnd) = GetDateRange();
        var @params = new StatisticsParams
        {
            IdentityId = identityId,
            DateStart = dateStart,
            DateEnd = dateEnd,
        };

        try
        {
            // Summary
            var summary = StatisticsService.GetSummary(@params);
            TotalExpense = summary.TotalExpense.ToString("F2");
            TotalIncome = summary.TotalIncome.ToString("F2");
            NetExpense = summary.NetExpense.ToString("F2");
            DailyAverage = summary.DailyAverage.ToString("F2");
            ExpenseCount = summary.ExpenseCount;
            IncomeCount = summary.IncomeCount;

            // Daily trend - grouped column chart
            var trend = StatisticsService.GetDailyTrend(@params);
            BuildDailyTrendChart(trend);

            // Category distribution - horizontal bar + pie
            var categories = StatisticsService.GetCategoryDistribution(@params);
            BuildCategoryCharts(categories);

            // Meal distribution - column chart
            var meals = StatisticsService.GetMealDistribution(@params);
            BuildMealChart(meals);

            // Consumption buckets - column chart
            var buckets = StatisticsService.GetConsumptionDistribution(@params);
            BuildConsumptionChart(buckets);

            // Merchant ranking - horizontal bar chart
            var merchants = StatisticsService.GetMerchantRanking(@params);
            BuildMerchantChart(merchants);

            // Month comparison - grouped column chart
            LoadMonthComparison(identityId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载统计数据失败: {ex.Message}";
        }
    }

    private void BuildDailyTrendChart(List<DailyTrendItem> trend)
    {
        var dateLabels = trend.Select(t =>
        {
            // Show short date: MM/dd or just dd
            var parts = t.Date.Split('-', '/');
            return parts.Length >= 3 ? $"{parts[1]}/{parts[2]}" : t.Date;
        }).ToList();

        var expenseValues = trend.Select(t => t.Expense).ToList();
        var incomeValues = trend.Select(t => t.Income).ToList();

        DailySeries.Clear();
        DailySeries.Add(new ColumnSeries<double>
        {
            Values = expenseValues,
            Name = "支出",
            Fill = new SolidColorPaint(ExpenseColor),
            Stroke = null,
            MaxBarWidth = 20,
        });
        DailySeries.Add(new ColumnSeries<double>
        {
            Values = incomeValues,
            Name = "收入",
            Fill = new SolidColorPaint(IncomeColor),
            Stroke = null,
            MaxBarWidth = 20,
        });

        DailyXAxes[0].Labels = dateLabels;
        DailyYAxes[0].Labeler = v => v.ToString("F0");
    }

    private void BuildCategoryCharts(List<CategoryItem> categories)
    {
        // Horizontal bar chart - category ranking
        var names = categories.Select(c => c.Name).ToList();
        var values = categories.Select(c => c.Value).ToList();

        CategorySeries.Clear();
        for (var i = 0; i < categories.Count; i++)
        {
            var color = ParseColor(categories[i].Color, i);
            CategorySeries.Add(new RowSeries<double>
            {
                Values = [categories[i].Value],
                Name = categories[i].Name,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 24,
            });
        }

        CategoryYAxes[0].Labels = names;
        CategoryXAxes[0].Labeler = v => v.ToString("F0");

        // Pie chart - category proportion
        CategoryPieSeries.Clear();
        for (var i = 0; i < categories.Count; i++)
        {
            var color = ParseColor(categories[i].Color, i);
            CategoryPieSeries.Add(new PieSeries<double>
            {
                Values = [categories[i].Value],
                Name = categories[i].Name,
                Fill = new SolidColorPaint(color),
                Stroke = null,
            });
        }
    }

    private void BuildMealChart(List<MealDistItem> meals)
    {
        var labels = meals.Select(m => m.Name).ToList();
        var amounts = meals.Select(m => m.Amount).ToList();

        MealSeries.Clear();
        for (var i = 0; i < meals.Count; i++)
        {
            var color = ChartColors[i % ChartColors.Length];
            MealSeries.Add(new ColumnSeries<double>
            {
                Values = [meals[i].Amount],
                Name = meals[i].Name,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 32,
            });
        }

        MealXAxes[0].Labels = labels;
        MealYAxes[0].Labeler = v => v.ToString("F0");
    }

    private void BuildConsumptionChart(List<ConsumptionBucketItem> buckets)
    {
        var labels = buckets.Select(b => b.Range).ToList();
        var amounts = buckets.Select(b => b.Amount).ToList();

        ConsumptionSeries.Clear();
        for (var i = 0; i < buckets.Count; i++)
        {
            var color = ChartColors[i % ChartColors.Length];
            ConsumptionSeries.Add(new ColumnSeries<double>
            {
                Values = [buckets[i].Amount],
                Name = buckets[i].Range,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 32,
            });
        }

        ConsumptionXAxes[0].Labels = labels;
        ConsumptionYAxes[0].Labeler = v => v.ToString("F0");
    }

    private void BuildMerchantChart(List<MerchantRankingItem> merchants)
    {
        var names = merchants.Select(m => m.Merchant).ToList();

        MerchantSeries.Clear();
        for (var i = 0; i < merchants.Count; i++)
        {
            var color = ChartColors[i % ChartColors.Length];
            MerchantSeries.Add(new RowSeries<double>
            {
                Values = [merchants[i].Amount],
                Name = merchants[i].Merchant,
                Fill = new SolidColorPaint(color),
                Stroke = null,
                MaxBarWidth = 24,
            });
        }

        MerchantYAxes[0].Labels = names;
        MerchantXAxes[0].Labeler = v => v.ToString("F0");
    }

    private void LoadMonthComparison(int identityId)
    {
        var now = DateTime.Now;
        var months = new List<string>();
        var expenses = new List<double>();
        var incomes = new List<double>();

        for (var i = 5; i >= 0; i--)
        {
            var month = now.AddMonths(-i);
            var monthStart = new DateTime(month.Year, month.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var @params = new StatisticsParams
            {
                IdentityId = identityId,
                DateStart = monthStart.ToString("yyyy-MM-dd"),
                DateEnd = monthEnd.ToString("yyyy-MM-dd"),
            };

            var summary = StatisticsService.GetSummary(@params);
            months.Add(month.ToString("yyyy/MM"));
            expenses.Add(summary.TotalExpense);
            incomes.Add(summary.TotalIncome);
        }

        MonthCompareSeries.Clear();
        MonthCompareSeries.Add(new ColumnSeries<double>
        {
            Values = expenses,
            Name = "支出",
            Fill = new SolidColorPaint(ExpenseColor),
            Stroke = null,
            MaxBarWidth = 20,
        });
        MonthCompareSeries.Add(new ColumnSeries<double>
        {
            Values = incomes,
            Name = "收入",
            Fill = new SolidColorPaint(IncomeColor),
            Stroke = null,
            MaxBarWidth = 20,
        });

        MonthCompareXAxes[0].Labels = months;
        MonthCompareYAxes[0].Labeler = v => v.ToString("F0");
    }

    /// <summary>
    /// Parse hex color string to SKColor, fallback to ChartColors palette by index
    /// </summary>
    private static SKColor ParseColor(string hexColor, int fallbackIndex)
    {
        try
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length == 6)
                return new SKColor(
                    byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
                    byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
                    byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
        }
        catch
        {
            // fallback below
        }
        return ChartColors[fallbackIndex % ChartColors.Length];
    }
}
