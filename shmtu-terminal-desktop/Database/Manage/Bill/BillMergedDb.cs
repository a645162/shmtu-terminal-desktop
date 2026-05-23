using System.Text.Json;
using shmtu.terminal.desktop.Database.Common;
using shmtu.terminal.desktop.Database.Source.Identity;
using shmtu.terminal.desktop.Models.Bill;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Bill;

public static class BillMergedDb
{
    private static SqlSugarClient GetDb(int identityId)
    {
        return new IdentityDbSource(identityId).GetNewDbObj();
    }

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
        var dir = Path.Combine(BaseDbSource.DataDirectoryPath, "identity");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// CRITICAL 4 fix: 带过滤条件的分页查询，将所有过滤下推到数据库层
    /// 返回 (记录列表, 总数)
    /// </summary>
    public static (List<BillMerged> Bills, int TotalCount) GetFiltered(
        int identityId,
        int pageIndex,
        int pageSize,
        long? startTime,
        long? endTime,
        string? searchText,
        string? classificationType)
    {
        var db = GetDb(identityId);
        var query = db.Queryable<BillMerged>();

        // 时间范围过滤
        if (startTime.HasValue)
            query = query.Where(b => b.Timestamp >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(b => b.Timestamp <= endTime.Value);

        // 搜索文本过滤（ItemType / TargetUser / Number）
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.ToLowerInvariant();
            query = query.Where(b =>
                b.ItemType!.Contains(search) ||
                b.TargetUser!.Contains(search) ||
                b.Number!.Contains(search));
        }

        // 分类类型过滤 — 内联表达式，避免 C# 方法无法转译 SQL
        if (!string.IsNullOrWhiteSpace(classificationType))
        {
            query = classificationType.ToLowerInvariant() switch
            {
                "充值" => query.Where(b => b.ItemType!.Contains("充值") || b.ItemType!.Contains("中行云充值") || b.ItemType!.Contains("微信充值")),
                "电费" => query.Where(b => b.ItemType!.Contains("电费")),
                "洗澡" => query.Where(b => b.ItemType!.Contains("淋浴") || b.ItemType!.Contains("热水")),
                "热水" => query.Where(b => b.ItemType!.Contains("水控转账")),
                "食堂" => query.Where(b => b.TargetUser!.Contains("食堂") || b.TargetUser!.Contains("餐厅")),
                "蛋糕" => query.Where(b => b.TargetUser!.Contains("西点房")),
                _ => query
            };
        }

        // 获取总数（不带排序和分页）
        var count = query.Count();

        // 分页 + 排序
        var bills = query
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);

        return (bills, count);
    }

    /// <summary>
    /// 查询所有合并账单（分页，无过滤）
    /// </summary>
    public static List<BillMerged> GetAll(int identityId, int pageIndex = 1, int pageSize = 50)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);
    }

    /// <summary>
    /// 查询所有合并账单（不分页，无过滤）
    /// </summary>
    public static List<BillMerged> GetAll(int identityId)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
    }

    public static List<BillMerged> GetByTimeRange(int identityId, long startTime, long endTime)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>()
            .Where(b => b.Timestamp >= startTime && b.Timestamp <= endTime)
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
    }

    public static BillMerged? GetById(int identityId, int id)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>().Where(b => b.Id == id).First();
    }

    public static bool ContainsByNumberList(int identityId, string numberListJson)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>().Where(b => b.NumberList == numberListJson).Any();
    }

    public static int Append(int identityId, BillMerged bill)
    {
        var db = GetDb(identityId);
        bill.SyncedAt = DateTime.Now.ToString("o");
        return db.Insertable(bill).ExecuteReturnIdentity();
    }

    public static void AppendRange(int identityId, List<BillMerged> bills)
    {
        if (bills.Count == 0) return;
        var db = GetDb(identityId);
        var now = DateTime.Now.ToString("o");
        foreach (var bill in bills) bill.SyncedAt = now;
        db.Insertable(bills).ExecuteCommand();
    }

    public static int AddManual(int identityId, BillMerged bill)
    {
        var db = GetDb(identityId);
        bill.IsManual = true;
        bill.SyncedAt = DateTime.Now.ToString("o");

        db.Ado.BeginTran();
        try
        {
            var id = db.Insertable(bill).ExecuteReturnIdentity();
            db.Insertable(new OperationLog
            {
                OperationType = "add",
                RecordNumbers = bill.NumberList,
                OperationTime = DateTime.Now.ToString("o"),
                Description = $"手动添加账单：{bill.ItemType} {bill.MoneyStr}",
                AccountId = bill.SourceAccountId,
            }).ExecuteCommand();
            db.Ado.CommitTran();
            return id;
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public static bool DeleteManual(int identityId, int id)
    {
        var db = GetDb(identityId);
        var bill = GetById(identityId, id);
        if (bill == null) return false;

        db.Ado.BeginTran();
        try
        {
            db.Deleteable<BillMerged>().Where(b => b.Id == id).ExecuteCommand();
            db.Insertable(new OperationLog
            {
                OperationType = "delete",
                RecordNumbers = bill.NumberList,
                OperationTime = DateTime.Now.ToString("o"),
                Description = $"手动删除账单：{bill.ItemType} {bill.MoneyStr}",
                AccountId = bill.SourceAccountId,
            }).ExecuteCommand();
            db.Ado.CommitTran();
            return true;
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public static bool Update(int identityId, BillMerged bill)
    {
        var db = GetDb(identityId);
        return db.Updateable(bill).ExecuteCommand() > 0;
    }

    public static int GetCount(int identityId)
    {
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>().Count();
    }

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
}
