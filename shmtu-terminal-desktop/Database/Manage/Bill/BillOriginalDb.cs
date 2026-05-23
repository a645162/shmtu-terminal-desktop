using System.Text.Json;
using shmtu.terminal.desktop.Database.Source.Bill;
using shmtu.terminal.desktop.Models.Bill;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Bill;

/// <summary>
/// 原始账单数据库 CRUD 操作 — 只读
/// 每个账号有独立的数据库文件：Data/account/<account_id>.sqlite
/// 表名：bill_original
/// </summary>
public static class BillOriginalDb
{
    private static SqlSugarClient GetDb(string accountId)
    {
        return new BillOriginalDbSource(accountId).GetNewDbObj();
    }

    /// <summary>
    /// 初始化账号的原始账单表
    /// </summary>
    public static void InitTable(string accountId)
    {
        EnsureDirectoryExists(accountId);
        var db = GetDb(accountId);
        if (!db.DbMaintenance.IsAnyTable(typeof(BillOriginal).FullName))
        {
            db.CodeFirst.InitTables(typeof(BillOriginal));
        }
    }

    /// <summary>
    /// 确保账号数据库目录存在
    /// </summary>
    private static void EnsureDirectoryExists(string accountId)
    {
        var dir = System.IO.Path.Combine(
            shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath, "account");
        if (!System.IO.Directory.Exists(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// 查询所有原始账单（分页）
    /// </summary>
    public static List<BillOriginal> GetAll(string accountId, int pageIndex = 1, int pageSize = 50)
    {
        var db = GetDb(accountId);
        return db.Queryable<BillOriginal>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);
    }

    /// <summary>
    /// 查询所有原始账单（不分页）
    /// </summary>
    public static List<BillOriginal> GetAll(string accountId)
    {
        var db = GetDb(accountId);
        return db.Queryable<BillOriginal>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
    }

    /// <summary>
    /// 根据时间范围查询
    /// </summary>
    public static List<BillOriginal> GetByTimeRange(string accountId, long startTime, long endTime)
    {
        var db = GetDb(accountId);
        return db.Queryable<BillOriginal>()
            .Where(b => b.Timestamp >= startTime && b.Timestamp <= endTime)
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
    }

    /// <summary>
    /// 判断交易号是否已存在
    /// </summary>
    public static bool Contains(string accountId, string number)
    {
        var db = GetDb(accountId);
        return db.Queryable<BillOriginal>()
            .Where(b => b.Number == number)
            .Any();
    }

    /// <summary>
    /// 判断交易号列表是否已存在（用于合并记录去重）
    /// </summary>
    public static bool ContainsByNumberList(string accountId, string numberListJson)
    {
        var db = GetDb(accountId);
        return db.Queryable<BillOriginal>()
            .Where(b => b.NumberList == numberListJson)
            .Any();
    }

    /// <summary>
    /// 插入新账单记录（同步时调用）
    /// </summary>
    public static int Insert(string accountId, BillOriginal bill)
    {
        var db = GetDb(accountId);
        bill.AccountId = accountId;
        bill.SyncedAt = DateTime.Now.ToString("o");
        return db.Insertable(bill).ExecuteReturnIdentity();
    }

    /// <summary>
    /// 批量插入新账单记录
    /// </summary>
    public static void InsertRange(string accountId, List<BillOriginal> bills)
    {
        if (bills.Count == 0) return;
        var db = GetDb(accountId);
        var now = DateTime.Now.ToString("o");
        foreach (var bill in bills)
        {
            bill.AccountId = accountId;
            bill.SyncedAt = now;
        }
        db.Insertable(bills).ExecuteCommand();
    }

    /// <summary>
    /// 获取总记录数
    /// </summary>
    public static int GetCount(string accountId)
    {
        var db = GetDb(accountId);
        return db.Queryable<BillOriginal>().Count();
    }

    /// <summary>
    /// 全量替换 — 清空并重新插入所有数据
    /// </summary>
    public static void FullReplace(string accountId, List<BillOriginal> bills)
    {
        var db = GetDb(accountId);
        db.Ado.BeginTran();
        try
        {
            db.Deleteable<BillOriginal>().ExecuteCommand();
            if (bills.Count > 0)
            {
                var now = DateTime.Now.ToString("o");
                foreach (var bill in bills)
                {
                    bill.AccountId = accountId;
                    bill.SyncedAt = now;
                }
                db.Insertable(bills).ExecuteCommand();
            }
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    #region BillItemInfo 与 BillOriginal 的转换

    /// <summary>
    /// 将 BillItemInfo（shmtu-dotnet-lib）转换为 BillOriginal（数据库模型）
    /// </summary>
    public static BillOriginal FromBillItemInfo(shmtu.datatype.bill.BillItemInfo item, string accountId)
    {
        return new BillOriginal
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
            AccountId = accountId,
        };
    }

    /// <summary>
    /// 将 BillOriginal 转换为 BillItemInfo（shmtu-dotnet-lib）
    /// </summary>
    public static shmtu.datatype.bill.BillItemInfo ToBillItemInfo(BillOriginal bill)
    {
        if (bill.IsCombined)
        {
            // 合并记录需要特殊处理
            var numberList = bill.NumberList != null
                ? JsonSerializer.Deserialize<List<string>>(bill.NumberList) ?? []
                : [];

            var items = new List<shmtu.datatype.bill.BillItemInfo>();
            foreach (var _ in numberList)
            {
                // 无法还原合并前的子条目，返回合并后的摘要
            }

            // 返回一个合并条目
            var merged = shmtu.datatype.bill.BillItemInfo.Merge(
                numberList.Select(n =>
                    new shmtu.datatype.bill.BillItemInfo(
                        bill.DateStr, bill.TimeStr,
                        bill.ItemType ?? "", n,
                        bill.TargetUser ?? "",
                        bill.MoneyStr ?? "0",
                        bill.Method ?? "", bill.StatusStr ?? ""
                    )).ToList()
            );
            return merged;
        }

        return new shmtu.datatype.bill.BillItemInfo(
            bill.DateStr, bill.TimeStr,
            bill.ItemType ?? "", bill.Number ?? "",
            bill.TargetUser ?? "", bill.MoneyStr ?? "0",
            bill.Method ?? "", bill.StatusStr ?? ""
        );
    }

    #endregion
}
