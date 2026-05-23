using shmtu.terminal.desktop.Database.Common;
using shmtu.terminal.desktop.Database.Source;
using shmtu.terminal.desktop.Models.Identity;
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
        var db = GetDb();
        if (!db.DbMaintenance.IsAnyTable(typeof(IdentityInfo).FullName))
        {
            db.CodeFirst.InitTables(typeof(IdentityInfo));
        }
    }

    /// <summary>
    /// 获取所有身份
    /// </summary>
    public static List<IdentityInfo> GetAll()
    {
        var db = GetDb();
        return db.Queryable<IdentityInfo>()
            .OrderBy(i => i.Id)
            .ToList();
    }

    /// <summary>
    /// 获取所有启用的身份
    /// </summary>
    public static List<IdentityInfo> GetEnabled()
    {
        var db = GetDb();
        return db.Queryable<IdentityInfo>()
            .Where(i => i.Enable)
            .OrderBy(i => i.Id)
            .ToList();
    }

    /// <summary>
    /// 根据ID获取身份
    /// </summary>
    public static IdentityInfo? GetById(int id)
    {
        var db = GetDb();
        return db.Queryable<IdentityInfo>()
            .Where(i => i.Id == id)
            .First();
    }

    /// <summary>
    /// 添加身份
    /// </summary>
    public static int Add(IdentityInfo identity)
    {
        var db = GetDb();
        identity.CreatedAt = DateTime.Now.ToString("o");
        identity.UpdatedAt = DateTime.Now.ToString("o");
        return db.Insertable(identity).ExecuteReturnIdentity();
    }

    /// <summary>
    /// 更新身份
    /// </summary>
    public static bool Update(IdentityInfo identity)
    {
        var db = GetDb();
        identity.UpdatedAt = DateTime.Now.ToString("o");
        return db.Updateable(identity).ExecuteCommand() > 0;
    }

    /// <summary>
    /// 删除身份（同时删除对应的数据库文件）
    /// </summary>
    public static bool Delete(int id)
    {
        var db = GetDb();
        // 先删除该身份下的所有账号
        var accounts = AccountDb.GetByIdentityId(id);
        foreach (var account in accounts)
        {
            AccountDb.Delete(account.Id);
        }

        // 删除身份数据库文件
        var identityDbPath = System.IO.Path.Combine(
            BaseDbSource.DataDirectoryPath, "identity", $"{id}.sqlite");
        if (System.IO.File.Exists(identityDbPath))
        {
            System.IO.File.Delete(identityDbPath);
        }

        return db.Deleteable<IdentityInfo>()
            .Where(i => i.Id == id)
            .ExecuteCommand() > 0;
    }

    /// <summary>
    /// 设置默认身份
    /// </summary>
    public static void SetDefaultIdentity(int id, bool remember)
    {
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
    }

    /// <summary>
    /// 获取默认身份
    /// </summary>
    public static IdentityInfo? GetDefaultIdentity()
    {
        var db = GetDb();
        return db.Queryable<IdentityInfo>()
            .Where(i => i.DefaultRemember)
            .First();
    }
}
