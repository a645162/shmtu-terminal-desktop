using shmtu.terminal.desktop.Services;
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
        LoggingService.Debug("[BillOriginalDb] 初始化原始账单表 | AccountId={AccountId}", accountId);
        EnsureDirectoryExists(accountId);
        var db = GetDb(accountId);
        if (!db.DbMaintenance.IsAnyTable(typeof(BillOriginal).FullName))
        {
            LoggingService.Information("[BillOriginalDb] 创建原始账单表 | AccountId={AccountId}", accountId);
            db.CodeFirst.InitTables(typeof(BillOriginal));
        }
        else
        {
            LoggingService.Debug("[BillOriginalDb] 原始账单表已存在 | AccountId={AccountId}", accountId);
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
            LoggingService.Debug("[BillOriginalDb] 创建账号数据库目录 | Path={Path}", dir);
            System.IO.Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// 查询所有原始账单（分页）
    /// </summary>
    public static List<BillOriginal> GetAll(string accountId, int pageIndex = 1, int pageSize = 50)
    {
        LoggingService.Verbose("[BillOriginalDb] 分页查询原始账单 | AccountId={AccountId} | Page={Page} | Size={Size}",
            accountId, pageIndex, pageSize);
        var db = GetDb(accountId);
        var result = db.Queryable<BillOriginal>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);
        LoggingService.Debug("[BillOriginalDb] 原始账单查询完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 查询所有原始账单（不分页）
    /// </summary>
    public static List<BillOriginal> GetAll(string accountId)
    {
        LoggingService.Debug("[BillOriginalDb] 查询所有原始账单 | AccountId={AccountId}", accountId);
        var db = GetDb(accountId);
        var result = db.Queryable<BillOriginal>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
        LoggingService.Information("[BillOriginalDb] 原始账单查询完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 根据时间范围查询
    /// </summary>
    public static List<BillOriginal> GetByTimeRange(string accountId, long startTime, long endTime)
    {
        LoggingService.Debug("[BillOriginalDb] 时间范围查询 | AccountId={AccountId} | Start={Start} | End={End}",
            accountId, startTime, endTime);
        var db = GetDb(accountId);
        var result = db.Queryable<BillOriginal>()
            .Where(b => b.Timestamp >= startTime && b.Timestamp <= endTime)
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
        LoggingService.Debug("[BillOriginalDb] 时间范围查询完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 判断交易号是否已存在
    /// </summary>
    public static bool Contains(string accountId, string number)
    {
        LoggingService.Verbose("[BillOriginalDb] 检查交易号是否存在 | AccountId={AccountId} | Number={Number}",
            accountId, number);
        var db = GetDb(accountId);
        var result = db.Queryable<BillOriginal>()
            .Where(b => b.Number == number)
            .Any();
        LoggingService.Verbose("[BillOriginalDb] 交易号存在检查完成 | Exists={Exists}", result);
        return result;
    }

    /// <summary>
    /// 判断交易号列表是否已存在（用于合并记录去重）
    /// </summary>
    public static bool ContainsByNumberList(string accountId, string numberListJson)
    {
        LoggingService.Debug("[BillOriginalDb] 检查交易号列表是否存在 | AccountId={AccountId}", accountId);
        var db = GetDb(accountId);
        var result = db.Queryable<BillOriginal>()
            .Where(b => b.NumberList == numberListJson)
            .Any();
        LoggingService.Debug("[BillOriginalDb] 交易号列表存在检查完成 | Exists={Exists}", result);
        return result;
    }

    /// <summary>
    /// 插入新账单记录（同步时调用）
    /// </summary>
    public static int Insert(string accountId, BillOriginal bill)
    {
        LoggingService.Debug("[BillOriginalDb] 插入原始账单 | AccountId={AccountId} | Number={Number}",
            accountId, bill.Number);
        var db = GetDb(accountId);
        bill.AccountId = accountId;
        bill.SyncedAt = DateTime.Now.ToString("o");
        var result = db.Insertable(bill).ExecuteReturnIdentity();
        LoggingService.Debug("[BillOriginalDb] 原始账单插入成功 | Id={Id}", result);
        return result;
    }

    /// <summary>
    /// 批量插入新账单记录
    /// </summary>
    public static void InsertRange(string accountId, List<BillOriginal> bills)
    {
        if (bills.Count == 0)
        {
            LoggingService.Verbose("[BillOriginalDb] 批量插入为空，跳过");
            return;
        }

        LoggingService.Information("[BillOriginalDb] 批量插入原始账单 | AccountId={AccountId} | Count={Count}",
            accountId, bills.Count);
        var db = GetDb(accountId);
        var now = DateTime.Now.ToString("o");
        foreach (var bill in bills)
        {
            bill.AccountId = accountId;
            bill.SyncedAt = now;
        }
        db.Insertable(bills).ExecuteCommand();
        LoggingService.Information("[BillOriginalDb] 批量插入完成 | Count={Count}", bills.Count);
    }

    /// <summary>
    /// 获取总记录数
    /// </summary>
    public static int GetCount(string accountId)
    {
        LoggingService.Verbose("[BillOriginalDb] 获取原始账单总数 | AccountId={AccountId}", accountId);
        var db = GetDb(accountId);
        var result = db.Queryable<BillOriginal>().Count();
        LoggingService.Debug("[BillOriginalDb] 原始账单总数 | Count={Count}", result);
        return result;
    }

    /// <summary>
    /// 全量替换 — 清空并重新插入所有数据
    /// </summary>
    public static void FullReplace(string accountId, List<BillOriginal> bills)
    {
        LoggingService.Warning("[BillOriginalDb] 执行全量替换 | AccountId={AccountId} | Count={Count}",
            accountId, bills.Count);
        var db = GetDb(accountId);
        db.Ado.BeginTran();
        try
        {
            var deleteCount = db.Deleteable<BillOriginal>().ExecuteCommand();
            LoggingService.Debug("[BillOriginalDb] 删除旧数据 | Count={Count}", deleteCount);

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
            LoggingService.Information("[BillOriginalDb] 全量替换完成 | InsertCount={Count}", bills.Count);
        }
        catch (Exception ex)
        {
            db.Ado.RollbackTran();
            LoggingService.Error(ex, "[BillOriginalDb] 全量替换失败，已回滚");
            throw;
        }
    }

    #region BillItemInfo 与 BillOriginal 的转换

    /// <summary>
    /// 将 BillItemInfo（shmtu-dotnet-lib）转换为 BillOriginal（数据库模型）
    /// </summary>
    public static BillOriginal FromBillItemInfo(
        shmtu.datatype.bill.BillItemInfo item,
        string accountId,
        string? building = null,
        string? room = null)
    {
        LoggingService.Verbose("[BillOriginalDb] 转换 BillItemInfo 到 BillOriginal | AccountId={AccountId} | Number={Number}",
            accountId, item.Number);
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
            Building = building,
            Room = room,
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
        LoggingService.Verbose("[BillOriginalDb] 转换 BillOriginal 到 BillItemInfo | Number={Number} | IsCombined={IsCombined}",
            bill.Number, bill.IsCombined);

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
