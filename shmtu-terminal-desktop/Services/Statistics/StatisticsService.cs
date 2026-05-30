using System.Text.RegularExpressions;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Services.BillClassification;

namespace shmtu.terminal.desktop.Services.Statistics;

/// <summary>
/// 统计服务 — 提供账单数据的各种统计分析功能
/// 对应 Tauri 的 commands/statistics.rs
/// </summary>
public static class StatisticsService
{
    private static readonly string[] CategoryColors =
    [
        "#5B8FF9", "#5AD8A6", "#F6BD16", "#E86452", "#6DC8EC",
        "#945FB9", "#FF9845", "#1E9493", "#FF99C3", "#269A99",
    ];

    private static readonly (string Range, int Index)[] ConsumptionBuckets =
    [
        ("<10元", 0),
        ("10-20元", 1),
        ("20-50元", 2),
        ("50-100元", 3),
        (">100元", 4),
    ];

    /// <summary>
    /// Income keywords matching Rust's INCOME_KEYWORDS
    /// </summary>
    private static readonly string[] IncomeKeywords = ["充值", "冲正", "退款", "返还", "补偿"];

    /// <summary>
    /// 获取统计汇总
    /// </summary>
    public static StatisticsSummary GetSummary(StatisticsParams @params)
    {
        LoggingService.Debug("[Statistics] GetSummary | IdentityId={IdentityId} | Start={Start} | End={End}",
            @params.IdentityId, @params.DateStart, @params.DateEnd);

        var bills = GetFilteredBills(@params);
        double totalExpense = 0, totalIncome = 0;
        int expenseCount = 0, incomeCount = 0;

        foreach (var bill in bills)
        {
            if (bill.StatusStr != "交易成功") continue;

            var money = Math.Abs(bill.Money ?? 0);
            var isIncome = IsIncomeBill(bill);

            if (isIncome)
            {
                totalIncome += money;
                incomeCount++;
            }
            else
            {
                totalExpense += money;
                expenseCount++;
            }
        }

        var uniqueDates = bills.Where(b => b.StatusStr == "交易成功")
            .Select(b => b.DateStr).Distinct().Count();
        var days = Math.Max(uniqueDates, 1);

        var result = new StatisticsSummary
        {
            TotalExpense = totalExpense,
            TotalIncome = totalIncome,
            NetExpense = totalExpense - totalIncome,
            DailyAverage = totalExpense / days,
            ExpenseCount = expenseCount,
            IncomeCount = incomeCount,
        };

        LoggingService.Information("[Statistics] GetSummary 完成 | Expense={Expense} | Income={Income} | Days={Days} | DailyAvg={DailyAvg}",
            result.TotalExpense, result.TotalIncome, days, result.DailyAverage);

        return result;
    }

    /// <summary>
    /// 获取每日趋势数据
    /// </summary>
    public static List<DailyTrendItem> GetDailyTrend(StatisticsParams @params)
    {
        LoggingService.Debug("[Statistics] GetDailyTrend | IdentityId={IdentityId}", @params.IdentityId);

        var bills = GetFilteredBills(@params);
        var dailyMap = new Dictionary<string, (double Expense, double Income)>();

        foreach (var bill in bills)
        {
            if (bill.StatusStr != "交易成功") continue;

            var money = Math.Abs(bill.Money ?? 0);
            var key = bill.DateStr;
            var isIncome = IsIncomeBill(bill);

            if (dailyMap.TryGetValue(key, out var current))
            {
                if (isIncome)
                    dailyMap[key] = (current.Expense, current.Income + money);
                else
                    dailyMap[key] = (current.Expense + money, current.Income);
            }
            else
            {
                dailyMap[key] = isIncome ? (0, money) : (money, 0);
            }
        }

        var result = dailyMap
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new DailyTrendItem
            {
                Date = kvp.Key,
                Expense = kvp.Value.Expense,
                Income = kvp.Value.Income,
            })
            .ToList();

        LoggingService.Information("[Statistics] GetDailyTrend 完成 | Days={Days}", result.Count);
        return result;
    }

    /// <summary>
    /// 获取分类分布
    /// </summary>
    public static List<CategoryItem> GetCategoryDistribution(StatisticsParams @params)
    {
        LoggingService.Debug("[Statistics] GetCategoryDistribution | IdentityId={IdentityId}", @params.IdentityId);

        var bills = GetFilteredBills(@params);
        var classifier = BillClassifierSingleton.Instance;
        var categoryMap = new Dictionary<string, (double Value, int Count)>();

        foreach (var bill in bills)
        {
            var classification = classifier.Classify(bill);
            var categoryName = classification.Type;
            var money = Math.Abs(bill.Money ?? 0);

            if (categoryMap.TryGetValue(categoryName, out var existing))
                categoryMap[categoryName] = (existing.Value + money, existing.Count + 1);
            else
                categoryMap[categoryName] = (money, 1);
        }

        var result = categoryMap
            .OrderByDescending(kvp => kvp.Value.Value)
            .Select((kvp, index) => new CategoryItem
            {
                Name = kvp.Key,
                Value = kvp.Value.Value,
                Count = kvp.Value.Count,
                Color = CategoryColors[index % CategoryColors.Length],
            })
            .ToList();

        LoggingService.Information("[Statistics] GetCategoryDistribution 完成 | Categories={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 获取餐食分布
    /// </summary>
    public static List<MealDistItem> GetMealDistribution(StatisticsParams @params)
    {
        LoggingService.Debug("[Statistics] GetMealDistribution | IdentityId={IdentityId}", @params.IdentityId);

        var bills = GetFilteredBills(@params);
        var classifier = BillClassifierSingleton.Instance;
        var mealMap = new Dictionary<string, (int Count, double Amount)>();

        foreach (var bill in bills)
        {
            var classification = classifier.Classify(bill);
            var mealName = classification.Meal ?? "非用餐时段";
            var money = Math.Abs(bill.Money ?? 0);

            if (mealMap.TryGetValue(mealName, out var existing))
                mealMap[mealName] = (existing.Count + 1, existing.Amount + money);
            else
                mealMap[mealName] = (1, money);
        }

        var result = mealMap
            .OrderByDescending(kvp => kvp.Value.Amount)
            .Select(kvp => new MealDistItem
            {
                Name = kvp.Key,
                Count = kvp.Value.Count,
                Amount = kvp.Value.Amount,
            })
            .ToList();

        LoggingService.Information("[Statistics] GetMealDistribution 完成 | Periods={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 获取消费区间分布
    /// </summary>
    public static List<ConsumptionBucketItem> GetConsumptionDistribution(StatisticsParams @params)
    {
        LoggingService.Debug("[Statistics] GetConsumptionDistribution | IdentityId={IdentityId}", @params.IdentityId);

        var bills = GetFilteredBills(@params);
        var bucketData = new (int Count, double Amount)[5];

        foreach (var bill in bills)
        {
            var money = Math.Abs(bill.Money ?? 0);
            var idx = money switch
            {
                < 10 => 0,
                < 20 => 1,
                < 50 => 2,
                < 100 => 3,
                _ => 4,
            };
            bucketData[idx].Count++;
            bucketData[idx].Amount += money;
        }

        var result = ConsumptionBuckets
            .Select(b => new ConsumptionBucketItem
            {
                Range = b.Range,
                Count = bucketData[b.Index].Count,
                Amount = bucketData[b.Index].Amount,
            })
            .ToList();

        LoggingService.Information("[Statistics] GetConsumptionDistribution 完成 | Buckets={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 获取商户排名（Top 10）
    /// </summary>
    public static List<MerchantRankingItem> GetMerchantRanking(StatisticsParams @params)
    {
        LoggingService.Debug("[Statistics] GetMerchantRanking | IdentityId={IdentityId}", @params.IdentityId);

        var bills = GetFilteredBills(@params);
        var merchantMap = new Dictionary<string, (double Amount, int Count)>();

        foreach (var bill in bills)
        {
            var target = bill.TargetUser;
            if (string.IsNullOrEmpty(target)) continue;

            var money = Math.Abs(bill.Money ?? 0);

            if (merchantMap.TryGetValue(target, out var existing))
                merchantMap[target] = (existing.Amount + money, existing.Count + 1);
            else
                merchantMap[target] = (money, 1);
        }

        var result = merchantMap
            .OrderByDescending(kvp => kvp.Value.Amount)
            .Take(10)
            .Select(kvp => new MerchantRankingItem
            {
                Merchant = kvp.Key,
                Amount = kvp.Value.Amount,
                Count = kvp.Value.Count,
            })
            .ToList();

        LoggingService.Information("[Statistics] GetMerchantRanking 完成 | Merchants={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 获取指定分类的汇总
    /// </summary>
    public static CategorySummary GetCategorySummary(CategorySummaryParams @params)
    {
        LoggingService.Debug("[Statistics] GetCategorySummary | IdentityId={IdentityId} | Category={Category}",
            @params.IdentityId, @params.Category);

        var identityParams = new StatisticsParams
        {
            IdentityId = @params.IdentityId,
            DateStart = @params.DateStart,
            DateEnd = @params.DateEnd,
        };
        var bills = GetFilteredBills(identityParams);
        var classifier = BillClassifierSingleton.Instance;
        var categoryName = @params.Category;
        double totalAmount = 0;
        int count = 0;

        foreach (var bill in bills)
        {
            var classification = classifier.Classify(bill);
            if (classification.Type == categoryName)
            {
                totalAmount += Math.Abs(bill.Money ?? 0);
                count++;
            }
        }

        var uniqueDates = bills.Select(b => b.DateStr).Distinct().Count();
        var days = Math.Max(uniqueDates, 1);

        var result = new CategorySummary
        {
            Category = categoryName,
            TotalAmount = totalAmount,
            Count = count,
            DailyAverage = totalAmount / days,
            AvgPerTransaction = count > 0 ? totalAmount / count : 0,
        };

        LoggingService.Information("[Statistics] GetCategorySummary 完成 | Total={Total} | Count={Count}",
            result.TotalAmount, result.Count);

        return result;
    }

    #region 私有辅助方法

    /// <summary>
    /// 判断账单是否为收入类型 — 匹配 Rust 的 INCOME_KEYWORDS
    /// 仅统计 status_str == "交易成功" 的记录
    /// </summary>
    private static bool IsIncomeBill(BillMerged bill)
    {
        var text = $"{bill.ItemType} {bill.TargetUser}";
        return IncomeKeywords.Any(kw => text.Contains(kw));
    }

    private static List<BillMerged> GetFilteredBills(StatisticsParams @params)
    {
        var startTime = @params.DateStart != null ? ParseDateToTimestamp(@params.DateStart) : (long?)null;
        var endTime = @params.DateEnd != null ? ParseDateToTimestamp(@params.DateEnd) : (long?)null;

        return BillMergedDb.GetFiltered(
            @params.IdentityId,
            1,
            int.MaxValue,
            startTime,
            endTime,
            null,
            null).Bills;
    }

    private static long ParseDateToTimestamp(string dateStr)
    {
        // 支持多种日期格式：2024.01.15, 2024-01-15, 2024/01/15
        var normalized = dateStr.Replace('.', '-').Replace('/', '-');

        // 尝试解析
        if (DateTime.TryParse(normalized, out var date))
        {
            return new DateTimeOffset(date).ToUnixTimeSeconds();
        }

        // 尝试解析不带前导零的格式
        var formats = new[] { "yyyy-M-d", "yyyy-MM-dd", "yyyy.M.d", "yyyy/M/d" };
        if (DateTime.TryParseExact(normalized, formats, null, System.Globalization.DateTimeStyles.None, out date))
        {
            return new DateTimeOffset(date).ToUnixTimeSeconds();
        }

        LoggingService.Warning("[Statistics] 日期解析失败，使用当前时间 | DateStr={DateStr}", dateStr);
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    #endregion
}

/// <summary>
/// BillClassifier 单例（避免重复加载规则文件）
/// </summary>
public static class BillClassifierSingleton
{
    private static BillClassifier? _instance;
    private static readonly object _lock = new();

    public static BillClassifier Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new BillClassifier();
                }
            }
            return _instance;
        }
    }

    public static void Reload()
    {
        lock (_lock)
        {
            _instance = new BillClassifier();
        }
    }
}