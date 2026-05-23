using SqlSugar;

namespace shmtu.terminal.desktop.Models.Identity;

/// <summary>
/// 账号信息 — 一个账号对应一个学号/校园卡，属于某个身份
/// 存储在主数据库 shmtu.terminal.sqlite 中
/// 密码加密存储
/// </summary>
[SugarTable("account")]
public class AccountInfo
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public int IdentityId { get; set; }

    [SugarColumn(IsNullable = false, Length = 100)]
    public string AccountName { get; set; } = "";

    [SugarColumn(IsNullable = false, Length = 12, UniqueGroupNameList = ["idx_account_id"])]
    public string AccountId { get; set; } = "";

    /// <summary>
    /// 密码（加密存储）
    /// </summary>
    [SugarColumn(IsNullable = false, Length = 500)]
    public string Password { get; set; } = "";

    [SugarColumn(IsNullable = false)]
    public bool Enable { get; set; } = true;

    [SugarColumn(IsNullable = false)]
    public bool EnableUpdate { get; set; } = true;

    [SugarColumn(IsNullable = false, Length = 20)]
    public string ExpireDate { get; set; } = "2099-12-31";

    [SugarColumn(IsNullable = false, Length = 30)]
    public string LastUpdateTime { get; set; } = "";

    [SugarColumn(IsNullable = false, Length = 30)]
    public string CreatedAt { get; set; } = DateTime.Now.ToString("o");

    [SugarColumn(IsNullable = false, Length = 30)]
    public string UpdatedAt { get; set; } = DateTime.Now.ToString("o");
}
