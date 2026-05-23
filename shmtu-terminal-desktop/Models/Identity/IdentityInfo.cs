using SqlSugar;

namespace shmtu.terminal.desktop.Models.Identity;

/// <summary>
/// 身份信息 — 一个身份代表一个"人"
/// 对应数据库文件：Data/identity/<id>.sqlite
/// </summary>
[SugarTable("identity")]
public class IdentityInfo
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false, Length = 100)]
    public string Name { get; set; } = "";

    [SugarColumn(IsNullable = false)]
    public bool Enable { get; set; } = true;

    [SugarColumn(IsNullable = false)]
    public bool EnableUpdate { get; set; } = true;

    [SugarColumn(IsNullable = true, Length = 20)]
    public string? Birthday { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool DefaultRemember { get; set; } = false;

    [SugarColumn(IsNullable = false, Length = 30)]
    public string CreatedAt { get; set; } = DateTime.Now.ToString("o");

    [SugarColumn(IsNullable = false, Length = 30)]
    public string UpdatedAt { get; set; } = DateTime.Now.ToString("o");
}
