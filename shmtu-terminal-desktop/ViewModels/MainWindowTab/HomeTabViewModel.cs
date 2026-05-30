using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Services.BillClassification;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.Services.Statistics;
using shmtu.terminal.desktop.ViewModels.Component;
using shmtu.terminal.desktop.ViewModels.Component.CountDown;

namespace shmtu.terminal.desktop.ViewModels.MainWindowTab;

/// <summary>
/// Display item for recent transactions on the home page
/// </summary>
public class RecentTransactionItem : ViewModelBase
{
    public string Name { get; set; } = "";
    public string Date { get; set; } = "";
    public string Amount { get; set; } = "";
    public string AmountColor { get; set; } = "#E86452";
}

/// <summary>
/// Display item for category summary on the home page
/// </summary>
public class CategorySummaryDisplayItem : ViewModelBase
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Color { get; set; } = "#5B8FF9";
}

public class HomeTabViewModel : ViewModelBase
{
    private string _todaySpending = "0.00";
    public string TodaySpending
    {
        get => _todaySpending;
        set => this.RaiseAndSetIfChanged(ref _todaySpending, value);
    }

    private string _todayComparison = "";
    public string TodayComparison
    {
        get => _todayComparison;
        set => this.RaiseAndSetIfChanged(ref _todayComparison, value);
    }

    private string _monthSpending = "0.00";
    public string MonthSpending
    {
        get => _monthSpending;
        set => this.RaiseAndSetIfChanged(ref _monthSpending, value);
    }

    private string _monthComparison = "";
    public string MonthComparison
    {
        get => _monthComparison;
        set => this.RaiseAndSetIfChanged(ref _monthComparison, value);
    }

    private string _monthDeposit = "0.00";
    public string MonthDeposit
    {
        get => _monthDeposit;
        set => this.RaiseAndSetIfChanged(ref _monthDeposit, value);
    }

    private string _cardBalance = "0.00";
    public string CardBalance
    {
        get => _cardBalance;
        set => this.RaiseAndSetIfChanged(ref _cardBalance, value);
    }

    public ChartViewModel WeeklyTrendChart { get; }
    public CountDownWeekViewModel CountdownViewModel { get; }
    public ObservableCollection<RecentTransactionItem> RecentTransactions { get; } = [];
    public ObservableCollection<CategorySummaryDisplayItem> CategorySummary { get; } = [];

    private bool _hasNoRecentTransactions = true;
    public bool HasNoRecentTransactions
    {
        get => _hasNoRecentTransactions;
        set => this.RaiseAndSetIfChanged(ref _hasNoRecentTransactions, value);
    }

    public ReactiveCommand<Unit, Unit> ShowTrendDetailCommand { get; }

    private int _identityId;
    public int IdentityId
    {
        get => _identityId;
        set
        {
            this.RaiseAndSetIfChanged(ref _identityId, value);
            RefreshData();
        }
    }

    public HomeTabViewModel()
    {
        WeeklyTrendChart = new ChartViewModel();
        CountdownViewModel = new CountDownWeekViewModel();

        ShowTrendDetailCommand = ReactiveCommand.Create(() =>
            NavigationService.Instance.SwitchTab(1));

        CountdownViewModel.ConfigureRequested += () =>
            NavigationService.Instance.OpenWindow("Settings",
                new ViewModels.Program.SettingsViewModel { SelectedSettingsIndex = 4 });
    }

    public void RefreshData()
    {
        if (_identityId <= 0) return;

        IsLoading = true;

        try
        {
            InitDb.InitIdentityDb(_identityId);

            var now = DateTime.Now;
            var today = now.Date;
            var todayStart = ((DateTimeOffset)today).ToUnixTimeSeconds();
            var todayEnd = ((DateTimeOffset)today.AddDays(1)).ToUnixTimeSeconds();
            var monthStart = ((DateTimeOffset)new DateTime(now.Year, now.Month, 1)).ToUnixTimeSeconds();
            var monthEnd = ((DateTimeOffset)today.AddDays(1)).ToUnixTimeSeconds();

            var todaySummary = BillMergedDb.GetSummary(_identityId, todayStart, todayEnd);
            TodaySpending = FormatMoney(-todaySummary.totalExpense);

            var monthSummary = BillMergedDb.GetSummary(_identityId, monthStart, monthEnd);
            MonthSpending = FormatMoney(-monthSummary.totalExpense);
            MonthDeposit = FormatMoney(monthSummary.totalIncome);

            var totalSummary = BillMergedDb.GetSummary(_identityId);
            CardBalance = FormatMoney(totalSummary.totalIncome + totalSummary.totalExpense);

            var yesterdayStart = ((DateTimeOffset)today.AddDays(-1)).ToUnixTimeSeconds();
            var yesterdaySummary = BillMergedDb.GetSummary(_identityId, yesterdayStart, todayStart);
            var diff = -todaySummary.totalExpense - (-yesterdaySummary.totalExpense);
            TodayComparison = diff >= 0 ? $"较昨日+{diff:F2}" : $"较昨日{diff:F2}";

            var lastMonthEnd = monthStart;
            var lastMonthStart = ((DateTimeOffset)new DateTime(now.Year, now.Month, 1).AddMonths(-1)).ToUnixTimeSeconds();
            var lastMonthSummary = BillMergedDb.GetSummary(_identityId, lastMonthStart, lastMonthEnd);
            var monthDiff = -monthSummary.totalExpense - (-lastMonthSummary.totalExpense);
            MonthComparison = monthDiff >= 0 ? $"较上月+{monthDiff:F2}" : $"较上月{monthDiff:F2}";

            UpdateWeeklyTrend();
            UpdateRecentTransactions();
            UpdateCategorySummary();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载首页数据失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateWeeklyTrend()
    {
        var data = new List<(string Label, double Value)>();
        var today = DateTime.Today;

        for (var i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayStart = ((DateTimeOffset)date).ToUnixTimeSeconds();
            var dayEnd = ((DateTimeOffset)date.AddDays(1)).ToUnixTimeSeconds();
            var summary = BillMergedDb.GetSummary(_identityId, dayStart, dayEnd);
            data.Add((date.ToString("MM/dd"), -summary.totalExpense));
        }

        WeeklyTrendChart.UpdateData(data);
    }

    /// <summary>
    /// Load the 5 most recent transactions for the home page
    /// </summary>
    private void UpdateRecentTransactions()
    {
        RecentTransactions.Clear();

        try
        {
            var (bills, _) = BillMergedDb.GetFiltered(_identityId, 1, 5, null, null, null, null);

            if (bills.Count == 0)
            {
                HasNoRecentTransactions = true;
                return;
            }

            HasNoRecentTransactions = false;

            // Income keyword detection matching Rust
            var incomeKeywords = new[] { "充值", "冲正", "退款", "返还", "补偿" };

            foreach (var bill in bills)
            {
                var isIncome = incomeKeywords.Any(kw =>
                    ($"{bill.ItemType} {bill.TargetUser}").Contains(kw));
                var money = bill.Money ?? 0;
                var moneyStr = isIncome ? $"+{Math.Abs(money):F2}" : $"{Math.Abs(money):F2}";

                RecentTransactions.Add(new RecentTransactionItem
                {
                    Name = bill.ItemType ?? "未知",
                    Date = bill.DateTimeFormatted ?? "",
                    Amount = moneyStr,
                    AmountColor = isIncome ? "#1A8F5A" : "#E86452",
                });
            }
        }
        catch
        {
            HasNoRecentTransactions = true;
        }
    }

    /// <summary>
    /// Load category distribution summary for the home page
    /// </summary>
    private void UpdateCategorySummary()
    {
        CategorySummary.Clear();

        try
        {
            var @params = new StatisticsParams { IdentityId = _identityId };
            var categories = StatisticsService.GetCategoryDistribution(@params);

            var categoryColors = new[]
            {
                "#5B8FF9", "#5AD8A6", "#F6BD16", "#E86452", "#6DC8EC",
                "#945FB9", "#FF9845", "#1E9493", "#FF99C3", "#269A99",
            };

            for (var i = 0; i < Math.Min(categories.Count, 6); i++)
            {
                var cat = categories[i];
                CategorySummary.Add(new CategorySummaryDisplayItem
                {
                    Name = cat.Name,
                    Value = $"{cat.Value:F2} ({cat.Count}笔)",
                    Color = categoryColors[i % categoryColors.Length],
                });
            }
        }
        catch
        {
            // Ignore errors loading category summary
        }
    }

    private static string FormatMoney(double amount) => $"{amount:F2}";
}
