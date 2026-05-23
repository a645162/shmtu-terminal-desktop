using SqlSugar;

namespace shmtu.terminal.desktop.Models.Bill;

/// <summary>
/// 操作记录 — 对身份数据库的手动操作记录，全量更新后清空
/// 存储在身份数据库中：Data/identity/<identity_id>.sqlite
/// 表名：operation_log
/// </summary>
[SugarTable("operation_log")]
public class OperationLog
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 操作类型：add / delete / merge
    /// </summary>
    [SugarColumn(IsNullable = false, Length = 20)]
    public string OperationType { get; set; } = "";

    /// <summary>
    /// 涉及的交易号列表（JSON数组）
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 5000, ColumnDataType = "text")]
    public string? RecordNumbers { get; set; }

    /// <summary>
    /// 操作时间（ISO 8601）
    /// </summary>
    [SugarColumn(IsNullable = false, Length = 30)]
    public string OperationTime { get; set; } = DateTime.Now.ToString("o");

    /// <summary>
    /// 操作描述
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 500)]
    public string? Description { get; set; }

    /// <summary>
    /// 关联账号
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 12)]
    public string? AccountId { get; set; }
}
