using System.Text.Json;
using shmtu.terminal.desktop.Database.Source.Identity;
using shmtu.terminal.desktop.Models.Bill;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Bill;

/// <summary>
/// 合并账单数据库 CRUD 操作 — 可增删改
/// 存储在身份数据库中：Data/identity/<identity_id>.sqlite
/// 表名：bill_merged
/// </summary>
public static class BillMergedDb
{
    private static SqlSugarClient GetDb(int identityId)
    {
        return new shmtu.terminal.desktop.Database.Source.Identity.IdentityDbSource(identityId).GetNewDbObj();
    }

    /// <summary>
    /// 初始化身份的合并账单表和操作记录表
    /// </summary>
    public static void InitTable(int identityId)
    {
        EnsureDirectoryExists(identityId);
        var db = GetDb(identityId);
        if (!db.DbMaintenance.IsAnyTable(typeof(BillMerged).FullName))
        {
            db.CodeFirst.InitTables(typeof(BillMerged));
        }
        if (!db.DbMaintenance.IsAnyTable(typeof(OperationLog).FullName))
        {
            db.CodeFirst.InitTables(typeof(OperationLog));
        }
    }

    private static void EnsureDirectoryExists(int identityId)
    {
        var dir = System.IO.Path.Combine(
            shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath, "identity");
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// 查询所有合并账单（分页）
    /// </summary>
    public static List<BillMerged> GetAll(int identityId, int pageIndex = 1, int pageSize = 50)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);
    }

    /// <summary>
    /// 查询所有合并账单（不分页）
    /// </summary>
    public static List<BillMerged> GetAll(int identityId)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
    }

    /// <summary>
    /// 根据时间范围查询
    /// </summary>
    public static List<BillMerged> GetByTimeRange(int identityId, long startTime, long endTime)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .Where(b => b.Timestamp >= startTime && b.Timestamp <= endTime)
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
    }

    /// <summary>
    /// 根据ID获取
    /// </summary>
    public static BillMerged? GetById(int identityId, int id)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .Where(b => b.Id == id)
            .First();
    }

    /// <summary>
    /// 判断交易号列表是否已存在
    /// </summary>
    public static bool ContainsByNumberList(int identityId, string numberListJson)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .Where(b => b.NumberList == numberListJson)
            .Any();
    }

    /// <summary>
    /// 追加新账单（自动同步时调用）
    /// </summary>
    public static int Append(int identityId, BillMerged bill)
    {
        var db = GetDb(identityId);
        bill.SyncedAt = DateTime.Now.ToString("o");
        return db.Insertable(bill).ExecuteReturnIdentity();
    }

    /// <summary>
    /// 批量追加新账单
    /// </summary>
    public static void AppendRange(int identityId, List<BillMerged> bills)
    {
        if (bills.Count == 0) return;
        var db = GetDb(identityId);
        var now = DateTime.Now.ToString("o");
        foreach (var bill in bills)
        {
            bill.SyncedAt = now;
        }
        db.Insertable(bills).ExecuteCommand();
    }

    /// <summary>
    /// 手动添加记录（记录操作日志）
    /// </summary>
    public static int AddManual(int identityId, BillMerged bill)
    {
        var db = GetDb(identityId);
        bill.IsManual = true;
        bill.SyncedAt = DateTime.Now.ToString("o");

        db.Ado.BeginTran();
        try
        {
            var id = db.Insertable(bill).ExecuteReturnIdentity();

            // 记录操作日志
            var operationLog = new OperationLog
            {
                OperationType = "add",
                RecordNumbers = bill.NumberList,
                OperationTime = DateTime.Now.ToString("o"),
                Description = $"手动添加账单：{bill.ItemType} {bill.MoneyStr}",
                AccountId = bill.SourceAccountId,
            };
            db.Insertable(operationLog).ExecuteCommand();

            db.Ado.CommitTran();
            return id;
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    /// <summary>
    /// 手动删除记录（记录操作日志）
    /// </summary>
    public static bool DeleteManual(int identityId, int id)
    {
        var db = GetDb(identityId);
        var bill = GetById(identityId, id);
        if (bill == null) return false;

        db.Ado.BeginTran();
        try
        {
            db.Deleteable<BillMerged>().Where(b => b.Id == id).ExecuteCommand();

            // 记录操作日志
            var operationLog = new OperationLog
            {
                OperationType = "delete",
                RecordNumbers = bill.NumberList,
                OperationTime = DateTime.Now.ToString("o"),
                Description = $"手动删除账单：{bill.ItemType} {bill.MoneyStr}",
                AccountId = bill.SourceAccountId,
            };
            db.Insertable(operationLog).ExecuteCommand();

            db.Ado.CommitTran();
            return true;
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    /// <summary>
    /// 更新记录
    /// </summary>
    public static bool Update(int identityId, BillMerged bill)
    {
        var db = GetDb(identityId);
        return db.Updateable(bill).ExecuteCommand() > 0;
    }

    /// <summary>
    /// 获取总记录数
    /// </summary>
    public static int GetCount(int identityId)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>().Count();
    }

    /// <summary>
    /// 获取统计摘要
    /// </summary>
    public static (double totalExpense, double totalIncome, int count) GetSummary(
        int identityId, long? startTime = null, long? endTime = null)
    {
        var db = GetDb(identityId);
        var query = db.Queryable<BillMerged>();

        if (startTime.HasValue)
            query = query.Where(b => b.Timestamp >= startTime);
        if (endTime.HasValue)
            query = query.Where(b => b.Timestamp <= endTime);

        var bills = query.ToList();
        double totalExpense = 0, totalIncome = 0;
        foreach (var bill in bills)
        {
            var money = bill.Money ?? 0;
            if (money < 0) totalExpense += money;
            else totalIncome += money;
        }
        return (totalExpense, totalIncome, bills.Count);
    }

    #region BillItemInfo 与 BillMerged 的转换

    /// <summary>
    /// 将 BillItemInfo（shmtu-dotnet-lib）转换为 BillMerged（数据库模型）
    /// </summary>
    public static BillMerged FromBillItemInfo(shmtu.datatype.bill.BillItemInfo item, string sourceAccountId)
    {
        return new BillMerged
        {
            DateStr = item.DateString,
            TimeStr = item.TimeString,
            TimeStrFormatted = item.TimeString.Length == 6
                ? $"{item.TimeString[..2]}:{item.TimeString[2..4]}:{item.TimeString[4..6]}"
                : item.TimeString,
            DateTimeFormatted = item.DateTimeStringFormated,
            EndDateTimeFormatted = item.EndDateTimeStringFormatted,
            Timestamp = item.TimeStamp,
            EndTimestamp = item.EndTimeStamp,
            ItemType = item.ItemType,
            Number = item.Number,
            NumberList = JsonSerializer.Serialize(item.NumberList),
            TargetUser = item.TargetUser,
            MoneyStr = item.MoneyString,
            Money = item.Money,
            Method = item.Method,
            StatusStr = item.StatusString,
            IsCombined = item.IsCombined,
            SourceAccountId = sourceAccountId,
            IsManual = false,
        };
    }

    /// <summary>
    /// 将 BillOriginal 转换为 BillMerged（跨数据库转换）
    /// </summary>
    public static BillMerged FromBillOriginal(BillOriginal original)
    {
        return new BillMerged
        {
            DateStr = original.DateStr,
            TimeStr = original.TimeStr,
            TimeStrFormatted = original.TimeStrFormatted,
            DateTimeFormatted = original.DateTimeFormatted,
            EndDateTimeFormatted = original.EndDateTimeFormatted,
            Timestamp = original.Timestamp,
            EndTimestamp = original.EndTimestamp,
            ItemType = original.ItemType,
            Number = original.Number,
            NumberList = original.NumberList,
            TargetUser = original.TargetUser,
            MoneyStr = original.MoneyStr,
            Money = original.Money,
            Method = original.Method,
            StatusStr = original.StatusStr,
            IsCombined = original.IsCombined,
            SourceAccountId = original.AccountId,
            IsManual = false,
        };
    }

    #endregion
}
