using shmtu.terminal.desktop.Services;
using shmtu.terminal.desktop.Database.Common;
using shmtu.terminal.desktop.Database.Source;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Services.Security;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Identity;

/// <summary>
/// 身份数据库 CRUD 操作
/// 身份信息存储在主数据库 shmtu.terminal.sqlite 中
/// </summary>
public static class IdentityDb
{
    private static SqlSugarClient GetDb() => BaseDbSource.GetNewDb();

    /// <summary>
    /// 初始化身份表
    /// </summary>
    public static void InitTable()
    {
        LoggingService.Debug("[IdentityDb] 初始化身份表");
        var db = GetDb();
        if (!db.DbMaintenance.IsAnyTable(typeof(IdentityInfo).FullName))
        {
            LoggingService.Information("[IdentityDb] 创建身份表");
            db.CodeFirst.InitTables(typeof(IdentityInfo));
        }
        else
        {
            LoggingService.Debug("[IdentityDb] 身份表已存在，跳过创建");
        }
    }

    /// <summary>
    /// 获取所有身份
    /// </summary>
    public static List<IdentityInfo> GetAll()
    {
        LoggingService.Verbose("[IdentityDb] 获取所有身份");
        var db = GetDb();
        var result = db.Queryable<IdentityInfo>()
            .OrderBy(i => i.Id)
            .ToList();
        LoggingService.Debug("[IdentityDb] 获取所有身份完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 获取所有启用的身份
    /// </summary>
    public static List<IdentityInfo> GetEnabled()
    {
        LoggingService.Debug("[IdentityDb] 获取所有启用的身份");
        var db = GetDb();
        var result = db.Queryable<IdentityInfo>()
            .Where(i => i.Enable)
            .OrderBy(i => i.Id)
            .ToList();
        LoggingService.Debug("[IdentityDb] 获取启用的身份完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 根据ID获取身份
    /// </summary>
    public static IdentityInfo? GetById(int id)
    {
        LoggingService.Verbose("[IdentityDb] 根据ID获取身份 | Id={Id}", id);
        var db = GetDb();
        var result = db.Queryable<IdentityInfo>()
            .Where(i => i.Id == id)
            .First();
        LoggingService.Verbose("[IdentityDb] 获取结果 | Found={Found} | Name={Name}", result != null, result?.Name);
        return result;
    }

    /// <summary>
    /// 添加身份
    /// </summary>
    public static int Add(IdentityInfo identity)
    {
        LoggingService.Information("[IdentityDb] 添加身份 | Name={Name} | StudentId={StudentId}",
            identity.Name);
        var db = GetDb();
        identity.CreatedAt = DateTime.Now.ToString("o");
        identity.UpdatedAt = DateTime.Now.ToString("o");
        var result = db.Insertable(identity).ExecuteReturnIdentity();
        LoggingService.Information("[IdentityDb] 身份添加成功 | Id={Id}", result);
        return result;
    }

    /// <summary>
    /// 更新身份
    /// </summary>
    public static bool Update(IdentityInfo identity)
    {
        LoggingService.Information("[IdentityDb] 更新身份 | Id={Id} | Name={Name}", identity.Id, identity.Name);
        var db = GetDb();
        identity.UpdatedAt = DateTime.Now.ToString("o");
        var result = db.Updateable(identity).ExecuteCommand() > 0;
        LoggingService.Information("[IdentityDb] 身份更新完成 | Success={Success}", result);
        return result;
    }

    /// <summary>
    /// 删除身份（同时删除对应的数据库文件）
    /// </summary>
    public static bool Delete(int id)
    {
        LoggingService.Warning("[IdentityDb] 删除身份 | Id={Id}", id);

        var db = GetDb();
        // 先删除该身份下的所有账号
        var accounts = AccountDb.GetByIdentityId(id);
        LoggingService.Debug("[IdentityDb] 删除身份前先删除 {Count} 个关联账号", accounts.Count);
        foreach (var account in accounts)
        {
            AccountDb.Delete(account.Id);
        }

        // 删除身份数据库文件
        var identityDbPath = System.IO.Path.Combine(
            BaseDbSource.DataDirectoryPath, "identity", $"{id}.sqlite");
        if (System.IO.File.Exists(identityDbPath))
        {
            LoggingService.Debug("[IdentityDb] 删除身份数据库文件 | Path={Path}", identityDbPath);
            System.IO.File.Delete(identityDbPath);
        }

        var result = db.Deleteable<IdentityInfo>()
            .Where(i => i.Id == id)
            .ExecuteCommand() > 0;
        LoggingService.Information("[IdentityDb] 身份删除完成 | Success={Success}", result);
        return result;
    }

    /// <summary>
    /// 设置默认身份
    /// </summary>
    public static void SetDefaultIdentity(int id, bool remember)
    {
        LoggingService.Information("[IdentityDb] 设置默认身份 | Id={Id} | Remember={Remember}", id, remember);
        var db = GetDb();
        // 清除所有默认
        db.Updateable<IdentityInfo>()
            .SetColumns(i => i.DefaultRemember == false)
            .ExecuteCommand();

        if (remember)
        {
            db.Updateable<IdentityInfo>()
                .SetColumns(i => i.DefaultRemember == true)
                .Where(i => i.Id == id)
                .ExecuteCommand();
        }
        LoggingService.Information("[IdentityDb] 默认身份设置完成");
    }

    /// <summary>
    /// 获取默认身份
    /// </summary>
    public static IdentityInfo? GetDefaultIdentity()
    {
        LoggingService.Debug("[IdentityDb] 获取默认身份");
        var db = GetDb();
        var result = db.Queryable<IdentityInfo>()
            .Where(i => i.DefaultRemember)
            .First();
        LoggingService.Debug("[IdentityDb] 默认身份查询完成 | Found={Found}", result != null);
        return result;
    }
}
