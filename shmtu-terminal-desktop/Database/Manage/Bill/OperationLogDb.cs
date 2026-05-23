using shmtu.terminal.desktop.Database.Source.Identity;
using shmtu.terminal.desktop.Models.Bill;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Bill;

/// <summary>
/// 操作记录数据库 CRUD 操作
/// 存储在身份数据库中：Data/identity/<identity_id>.sqlite
/// 表名：operation_log
/// 全量更新后清空
/// </summary>
public static class OperationLogDb
{
    private static SqlSugarClient GetDb(int identityId)
    {
        return new IdentityDbSource(identityId).GetNewDbObj();
    }

    /// <summary>
    /// 获取所有操作记录
    /// </summary>
    public static List<OperationLog> GetAll(int identityId)
    {
        var db = GetDb(identityId);
        return db.Queryable<OperationLog>()
            .OrderBy(o => o.Id, OrderByType.Desc)
            .ToList();
    }

    /// <summary>
    /// 根据账号筛选操作记录
    /// </summary>
    public static List<OperationLog> GetByAccountId(int identityId, string accountId)
    {
        var db = GetDb(identityId);
        return db.Queryable<OperationLog>()
            .Where(o => o.AccountId == accountId)
            .OrderBy(o => o.Id, OrderByType.Desc)
            .ToList();
    }

    /// <summary>
    /// 添加操作记录
    /// </summary>
    public static int Add(int identityId, OperationLog log)
    {
        var db = GetDb(identityId);
        log.OperationTime = DateTime.Now.ToString("o");
        return db.Insertable(log).ExecuteReturnIdentity();
    }

    /// <summary>
    /// 清空所有操作记录（全量更新后调用）
    /// </summary>
    public static void ClearAll(int identityId)
    {
        var db = GetDb(identityId);
        db.Deleteable<OperationLog>().ExecuteCommand();
    }

    /// <summary>
    /// 清空指定账号的操作记录
    /// </summary>
    public static void ClearByAccountId(int identityId, string accountId)
    {
        var db = GetDb(identityId);
        db.Deleteable<OperationLog>()
            .Where(o => o.AccountId == accountId)
            .ExecuteCommand();
    }

    /// <summary>
    /// 获取操作记录数量
    /// </summary>
    public static int GetCount(int identityId)
    {
        var db = GetDb(identityId);
        return db.Queryable<OperationLog>().Count();
    }
}
