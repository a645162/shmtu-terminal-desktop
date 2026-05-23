using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.Config;

namespace shmtu.terminal.desktop.Services.BillClassification;

/// <summary>
/// 账单分类引擎 — 根据 TOML 规则对账单进行分类
/// 支持类型分类、位置映射、用餐时段分类
/// </summary>
public class BillClassifier
{
    private ClassificationRules _rules;
    private DateTime _rulesLoadTime;

    public BillClassifier()
    {
        _rules = TomlConfigService.LoadClassificationRules();
        _rulesLoadTime = DateTime.Now;
    }

    /// <summary>
    /// 重新加载分类规则
    /// </summary>
    public void ReloadRules()
    {
        _rules = TomlConfigService.LoadClassificationRules();
        _rulesLoadTime = DateTime.Now;
    }

    /// <summary>
    /// 对单条账单进行分类
    /// </summary>
    public ClassificationResult Classify(BillMerged bill)
    {
        var result = new ClassificationResult
        {
            OriginalTargetUser = bill.TargetUser,
        };

        // ① 类型分类
        result.Type = ClassifyType(bill.ItemType ?? "", bill.TargetUser ?? "");

        // ② 位置映射
        var (building, room) = MapPosition(bill.TargetUser ?? "");
        result.Building = building;
        result.Room = room;

        // ③ 用餐时段分类
        result.Meal = ClassifyMealTime(bill.Timestamp);

        return result;
    }

    /// <summary>
    /// 对单条账单进行分类（使用 BillOriginal 模型）
    /// </summary>
    public ClassificationResult Classify(BillOriginal bill)
    {
        var result = new ClassificationResult
        {
            OriginalTargetUser = bill.TargetUser,
        };

        result.Type = ClassifyType(bill.ItemType ?? "", bill.TargetUser ?? "");
        var (building, room) = MapPosition(bill.TargetUser ?? "");
        result.Building = building;
        result.Room = room;
        result.Meal = ClassifyMealTime(bill.Timestamp);

        return result;
    }

    /// <summary>
    /// 对账单列表进行批量分类
    /// </summary>
    public List<(BillMerged Bill, ClassificationResult Result)> ClassifyAll(List<BillMerged> bills)
    {
        return bills.Select(b => (b, Classify(b))).ToList();
    }

    /// <summary>
    /// 获取当前分类规则
    /// </summary>
    public ClassificationRules GetRules() => _rules;

    #region 类型分类

    /// <summary>
    /// 类型分类 — 遍历 type 规则，首次匹配即返回
    /// </summary>
    private string ClassifyType(string itemType, string targetUser)
    {
        foreach (var kvp in _rules.Type)
        {
            var rule = kvp.Value;

            if (rule.MatchField == "item_type" || rule.MatchNames.Count > 0)
            {
                // 按 item_type 匹配
                if (rule.MatchNames.Any(name =>
                        itemType.Contains(name, StringComparison.OrdinalIgnoreCase)))
                {
                    return rule.Name;
                }
            }

            if (rule.MatchField == "target_user" || rule.MatchTargets.Count > 0)
            {
                // 按 target_user 匹配
                if (rule.MatchTargets.Any(target =>
                        targetUser.Contains(target, StringComparison.OrdinalIgnoreCase)))
                {
                    return rule.Name;
                }
            }
        }

        return "其他";
    }

    #endregion

    #region 位置映射

    /// <summary>
    /// 位置映射 — 精确匹配 target_user 字段
    /// </summary>
    private (string? building, string? room) MapPosition(string targetUser)
    {
        if (string.IsNullOrEmpty(targetUser)) return (null, null);

        // 精确匹配
        if (_rules.Position.Keywords.TryGetValue(targetUser, out var keyword))
        {
            return (keyword.Building, keyword.Room);
        }

        // 模糊匹配（关键词包含）
        foreach (var kvp in _rules.Position.Keywords)
        {
            if (targetUser.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return (kvp.Value.Building, kvp.Value.Room);
            }
        }

        return (null, null);
    }

    #endregion

    #region 用餐时段分类

    /// <summary>
    /// 用餐时段分类 — 根据时间戳判断用餐时段
    /// </summary>
    private string? ClassifyMealTime(long? timestamp)
    {
        if (!timestamp.HasValue) return null;

        var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).DateTime;
        var timeOfDay = dateTime.TimeOfDay;

        // 查找当前日期适用的 schedule
        foreach (var schedule in _rules.Schedule)
        {
            if (!IsScheduleValid(schedule, dateTime)) continue;

            foreach (var kvp in schedule.Timetable)
            {
                var meal = kvp.Value;
                var startTime = ParseTimeString(meal.StartTime);
                var endTime = ParseTimeString(meal.EndTime);

                if (startTime.HasValue && endTime.HasValue &&
                    timeOfDay >= startTime.Value && timeOfDay < endTime.Value)
                {
                    return meal.Name;
                }
            }
        }

        return "非用餐时段";
    }

    /// <summary>
    /// 判断 schedule 是否在有效期内
    /// </summary>
    private static bool IsScheduleValid(ScheduleRule schedule, DateTime date)
    {
        var startDate = ParseDateString(schedule.ValidDate.StartDate);
        if (startDate.HasValue && date < startDate.Value) return false;

        if (schedule.ValidDate.EndDate == "now") return true;

        var endDate = ParseDateString(schedule.ValidDate.EndDate);
        if (endDate.HasValue && date > endDate.Value) return false;

        return true;
    }

    /// <summary>
    /// 解析时间字符串（如 "6:30" → TimeSpan）
    /// </summary>
    private static TimeSpan? ParseTimeString(string timeStr)
    {
        if (string.IsNullOrEmpty(timeStr)) return null;

        var parts = timeStr.Split(':');
        if (parts.Length < 2) return null;

        if (int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes))
        {
            return new TimeSpan(hours, minutes, 0);
        }

        return null;
    }

    /// <summary>
    /// 解析日期字符串（如 "2019.9.1" → DateTime）
    /// </summary>
    private static DateTime? ParseDateString(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr == "now") return null;

        var normalized = dateStr.Replace('.', '-').Replace('_', '-');
        if (DateTime.TryParse(normalized, out var date))
            return date;

        return null;
    }

    #endregion

    #region 统计辅助方法

    /// <summary>
    /// 按类型统计金额
    /// </summary>
    public Dictionary<string, double> SumByType(List<(BillMerged Bill, ClassificationResult Result)> classified)
    {
        var result = new Dictionary<string, double>();
        foreach (var (bill, classification) in classified)
        {
            var type = classification.Type;
            var money = bill.Money ?? 0;
            result.TryGetValue(type, out var current);
            result[type] = current + money;
        }
        return result;
    }

    /// <summary>
    /// 按位置统计金额
    /// </summary>
    public Dictionary<string, double> SumByBuilding(List<(BillMerged Bill, ClassificationResult Result)> classified)
    {
        var result = new Dictionary<string, double>();
        foreach (var (bill, classification) in classified)
        {
            var building = classification.Building ?? "未知";
            var money = bill.Money ?? 0;
            result.TryGetValue(building, out var current);
            result[building] = current + money;
        }
        return result;
    }

    /// <summary>
    /// 按用餐时段统计金额
    /// </summary>
    public Dictionary<string, double> SumByMeal(List<(BillMerged Bill, ClassificationResult Result)> classified)
    {
        var result = new Dictionary<string, double>();
        foreach (var (bill, classification) in classified)
        {
            var meal = classification.Meal ?? "未知";
            var money = bill.Money ?? 0;
            result.TryGetValue(meal, out var current);
            result[meal] = current + money;
        }
        return result;
    }

    /// <summary>
    /// 按日期统计金额
    /// </summary>
    public Dictionary<string, double> SumByDate(List<BillMerged> bills)
    {
        var result = new Dictionary<string, double>();
        foreach (var bill in bills)
        {
            var date = bill.DateStr;
            var money = bill.Money ?? 0;
            result.TryGetValue(date, out var current);
            result[date] = current + money;
        }
        return result;
    }

    #endregion
}
