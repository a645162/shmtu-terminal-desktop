using SqlSugar;

namespace shmtu.terminal.desktop.Models.Bill;

/// <summary>
/// 合并账单记录 — 身份级别的合并账单
/// 由名下所有账号的原始数据自动同步而来，允许手动增删操作
/// 存储在身份数据库中：Data/identity/<identity_id>.sqlite
/// 表名：bill_merged
/// </summary>
[SugarTable("bill_merged")]
public class BillMerged
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
    /// 交易号列表（JSON数组）
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

    /// <summary>
    /// 来源学号（自动同步时填充）
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 12)]
    public string? SourceAccountId { get; set; }

    /// <summary>
    /// 是否手动添加的记录
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsManual { get; set; } = false;

    [SugarColumn(IsNullable = true, Length = 30)]
    public string? SyncedAt { get; set; }
}
