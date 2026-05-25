using System.Text.Json.Serialization;

namespace shmtu.terminal.desktop.Services.Statistics;

/// <summary>
/// 统计查询参数
/// </summary>
public class StatisticsParams
{
    [JsonPropertyName("identityId")]
    public int IdentityId { get; set; }

    [JsonPropertyName("dateStart")]
    public string? DateStart { get; set; }

    [JsonPropertyName("dateEnd")]
    public string? DateEnd { get; set; }
}

/// <summary>
/// 统计汇总
/// </summary>
public class StatisticsSummary
{
    [JsonPropertyName("totalExpense")]
    public double TotalExpense { get; set; }

    [JsonPropertyName("totalIncome")]
    public double TotalIncome { get; set; }

    [JsonPropertyName("netExpense")]
    public double NetExpense { get; set; }

    [JsonPropertyName("dailyAverage")]
    public double DailyAverage { get; set; }

    [JsonPropertyName("expenseCount")]
    public int ExpenseCount { get; set; }

    [JsonPropertyName("incomeCount")]
    public int IncomeCount { get; set; }
}

/// <summary>
/// 每日趋势项
/// </summary>
public class DailyTrendItem
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("expense")]
    public double Expense { get; set; }

    [JsonPropertyName("income")]
    public double Income { get; set; }
}

/// <summary>
/// 分类项
/// </summary>
public class CategoryItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "";
}

/// <summary>
/// 餐食分布项
/// </summary>
public class MealDistItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }
}

/// <summary>
/// 消费区间项
/// </summary>
public class ConsumptionBucketItem
{
    [JsonPropertyName("range")]
    public string Range { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }
}

/// <summary>
/// 商户排名项
/// </summary>
public class MerchantRankingItem
{
    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }
}

/// <summary>
/// 分类汇总参数
/// </summary>
public class CategorySummaryParams
{
    [JsonPropertyName("identityId")]
    public int IdentityId { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("dateStart")]
    public string? DateStart { get; set; }

    [JsonPropertyName("dateEnd")]
    public string? DateEnd { get; set; }
}

/// <summary>
/// 分类汇总结果
/// </summary>
public class CategorySummary
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("totalAmount")]
    public double TotalAmount { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("dailyAverage")]
    public double DailyAverage { get; set; }

    [JsonPropertyName("avgPerTransaction")]
    public double AvgPerTransaction { get; set; }
}