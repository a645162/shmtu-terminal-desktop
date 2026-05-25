using SqlSugar;

namespace shmtu.terminal.desktop.Models.Bill;

/// <summary>
/// 原始账单记录 — 从 API 获取，只读不允许修改
/// 每个账号有独立的数据库文件：Data/account/<account_id>.sqlite
/// 表名：bill_original
/// </summary>
[SugarTable("bill_original")]
public class BillOriginal
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false, Length = 20)]
    public string DateStr { get; set; } = "";

    [SugarColumn(IsNullable = false, Length = 20)]
    public string TimeStr { get; set; } = "";

    [SugarColumn(IsNullable = true, Length = 20)]
    public string? TimeStrFormatted { get; set; }

    [SugarColumn(IsNullable = true, Length = 30)]
    public string? DateTimeFormatted { get; set; }

    [SugarColumn(IsNullable = true, Length = 30)]
    public string? EndDateTimeFormatted { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? Timestamp { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? EndTimestamp { get; set; }

    [SugarColumn(IsNullable = true, Length = 100)]
    public string? ItemType { get; set; }

    [SugarColumn(IsNullable = true, Length = 50)]
    public string? Number { get; set; }

    /// <summary>
    /// 交易号列表（JSON数组，合并记录用）
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 2000, ColumnDataType = "text")]
    public string? NumberList { get; set; }

    [SugarColumn(IsNullable = true, Length = 200)]
    public string? TargetUser { get; set; }

    [SugarColumn(IsNullable = true, Length = 50)]
    public string? MoneyStr { get; set; }

    [SugarColumn(IsNullable = true)]
    public double? Money { get; set; }

    [SugarColumn(IsNullable = true, Length = 100)]
    public string? Method { get; set; }

    [SugarColumn(IsNullable = true, Length = 50)]
    public string? StatusStr { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool IsCombined { get; set; } = false;

    [SugarColumn(IsNullable = false, Length = 12)]
    public string AccountId { get; set; } = "";

    /// <summary>
    /// 翻译后的楼栋名称（如"海馨楼"、"海琴楼"）
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 100)]
    public string? Building { get; set; }

    /// <summary>
    /// 翻译后的具体房间/窗口
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 100)]
    public string? Room { get; set; }

    [SugarColumn(IsNullable = true, Length = 30)]
    public string? SyncedAt { get; set; }
}
