using System.Text.Json.Serialization;

namespace shmtu.terminal.desktop.Models.Config;

/// <summary>
/// 分类规则模型 — 对应 classification_rules.toml
/// </summary>
public class ClassificationRules
{
    /// <summary>
    /// 按消费类型分类
    /// Key: 类型标识（如 deposit, electricity, bath 等）
    /// </summary>
    public Dictionary<string, TypeRule> Type { get; set; } = [];

    /// <summary>
    /// 位置映射
    /// </summary>
    public PositionRule Position { get; set; } = new();

    /// <summary>
    /// 用餐时段列表
    /// </summary>
    public List<ScheduleRule> Schedule { get; set; } = [];
}

/// <summary>
/// 消费类型分类规则
/// </summary>
public class TypeRule
{
    /// <summary>
    /// 类型名称（如"充值"、"电费"）
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 匹配字段：item_type 或 target_user
    /// </summary>
    public string MatchField { get; set; } = "item_type";

    /// <summary>
    /// 匹配的交易名称列表（匹配 item_type 字段）
    /// </summary>
    public List<string> MatchNames { get; set; } = [];

    /// <summary>
    /// 匹配的目标用户列表（匹配 target_user 字段）
    /// </summary>
    public List<string> MatchTargets { get; set; } = [];
}

/// <summary>
/// 位置映射规则
/// </summary>
public class PositionRule
{
    /// <summary>
    /// 用于匹配的字段名（默认 target_user）
    /// </summary>
    public string Field { get; set; } = "target_user";

    /// <summary>
    /// 关键词 → 位置映射
    /// Key: 精确匹配的关键词（如"A食堂1楼大餐厅"）
    /// Value: 位置信息
    /// </summary>
    public Dictionary<string, PositionKeyword> Keywords { get; set; } = [];
}

/// <summary>
/// 位置关键词映射
/// </summary>
public class PositionKeyword
{
    public string Building { get; set; } = "";
    public string Room { get; set; } = "";
}

/// <summary>
/// 用餐时段规则
/// </summary>
public class ScheduleRule
{
    /// <summary>
    /// 有效日期范围
    /// </summary>
    public DateRange ValidDate { get; set; } = new();

    /// <summary>
    /// 用餐时间表
    /// Key: 时段标识（如 breakfast, lunch, dinner, midnight_snack）
    /// </summary>
    public Dictionary<string, MealPeriod> Timetable { get; set; } = [];
}

/// <summary>
/// 有效日期范围
/// </summary>
public class DateRange
{
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "now";
}

/// <summary>
/// 用餐时段
/// </summary>
public class MealPeriod
{
    public string Name { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
}

/// <summary>
/// 分类结果
/// </summary>
public class ClassificationResult
{
    /// <summary>
    /// 消费类型（充值/电费/洗澡/热水/蛋糕/食堂/其他）
    /// </summary>
    public string Type { get; set; } = "其他";

    /// <summary>
    /// 楼栋（如海馨楼、海琴楼、海联楼）
    /// </summary>
    public string? Building { get; set; }

    /// <summary>
    /// 具体位置/窗口
    /// </summary>
    public string? Room { get; set; }

    /// <summary>
    /// 用餐时段（早餐/午餐/晚餐/夜宵/非用餐时段）
    /// </summary>
    public string? Meal { get; set; }

    /// <summary>
    /// 原始对方账户/位置
    /// </summary>
    public string? OriginalTargetUser { get; set; }

    /// <summary>
    /// 获取显示名称（用于统计显示）
    /// </summary>
    public string DisplayName() => Type;
}
