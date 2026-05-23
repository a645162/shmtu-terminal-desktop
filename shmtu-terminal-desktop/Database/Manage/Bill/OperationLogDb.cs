using shmtu.terminal.desktop.Services;
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
        LoggingService.Debug("[OperationLogDb] 获取所有操作记录 | IdentityId={IdentityId}", identityId);
        var db = GetDb(identityId);
        var result = db.Queryable<OperationLog>()
            .OrderBy(o => o.Id, OrderByType.Desc)
            .ToList();
        LoggingService.Debug("[OperationLogDb] 操作记录查询完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 根据账号筛选操作记录
    /// </summary>
    public static List<OperationLog> GetByAccountId(int identityId, string accountId)
    {
        LoggingService.Debug("[OperationLogDb] 根据账号获取操作记录 | IdentityId={IdentityId} | AccountId={AccountId}",
            identityId, accountId);
        var db = GetDb(identityId);
        var result = db.Queryable<OperationLog>()
            .Where(o => o.AccountId == accountId)
            .OrderBy(o => o.Id, OrderByType.Desc)
            .ToList();
        LoggingService.Debug("[OperationLogDb] 操作记录查询完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 添加操作记录
    /// </summary>
    public static int Add(int identityId, OperationLog log)
    {
        LoggingService.Debug("[OperationLogDb] 添加操作记录 | IdentityId={IdentityId} | Type={Type}",
            identityId, log.OperationType);
        var db = GetDb(identityId);
        log.OperationTime = DateTime.Now.ToString("o");
        var result = db.Insertable(log).ExecuteReturnIdentity();
        LoggingService.Debug("[OperationLogDb] 操作记录添加成功 | Id={Id}", result);
        return result;
    }

    /// <summary>
    /// 清空所有操作记录（全量更新后调用）
    /// </summary>
    public static void ClearAll(int identityId)
    {
        LoggingService.Warning("[OperationLogDb] 清空所有操作记录 | IdentityId={IdentityId}", identityId);
        var db = GetDb(identityId);
        db.Deleteable<OperationLog>().ExecuteCommand();
        LoggingService.Information("[OperationLogDb] 操作记录已清空");
    }

    /// <summary>
    /// 清空指定账号的操作记录
    /// </summary>
    public static void ClearByAccountId(int identityId, string accountId)
    {
        LoggingService.Debug("[OperationLogDb] 清空指定账号的操作记录 | IdentityId={IdentityId} | AccountId={AccountId}",
            identityId, accountId);
        var db = GetDb(identityId);
        db.Deleteable<OperationLog>()
            .Where(o => o.AccountId == accountId)
            .ExecuteCommand();
        LoggingService.Information("[OperationLogDb] 账号操作记录已清空 | AccountId={AccountId}", accountId);
    }

    /// <summary>
    /// 获取操作记录数量
    /// </summary>
    public static int GetCount(int identityId)
    {
        LoggingService.Verbose("[OperationLogDb] 获取操作记录数量 | IdentityId={IdentityId}", identityId);
        var db = GetDb(identityId);
        var result = db.Queryable<OperationLog>().Count();
        LoggingService.Debug("[OperationLogDb] 操作记录数量 | Count={Count}", result);
        return result;
    }
}
