using SqlSugar;

namespace shmtu.terminal.desktop.Models.Session;

/// <summary>
/// 会话信息 — 存储账号的登录会话（cookies），加密存储
/// 存储在：Data/session.sqlite
/// 表名：session_info
/// </summary>
[SugarTable("session_info")]
public class SessionInfo
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 学号（唯一）
    /// </summary>
    [SugarColumn(IsNullable = false, Length = 12, UniqueGroupNameList = ["idx_account_id"])]
    public string AccountId { get; set; } = "";

    /// <summary>
    /// Cookies 数据（加密存储）
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDataType = "text")]
    public string Cookies { get; set; } = "";

    /// <summary>
    /// 登录时间
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 30)]
    public string? LoginTime { get; set; }

    /// <summary>
    /// 预估过期时间
    /// </summary>
    [SugarColumn(IsNullable = true, Length = 30)]
    public string? ExpireTime { get; set; }

    /// <summary>
    /// 是否仍有效
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public bool IsValid { get; set; } = true;
}
