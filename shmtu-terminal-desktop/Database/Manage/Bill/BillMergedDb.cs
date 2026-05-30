using shmtu.terminal.desktop.Services;
using System.Text.Json;
using shmtu.terminal.desktop.Database.Common;
using shmtu.terminal.desktop.Database.Source.Identity;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.classifier;
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
        LoggingService.Debug("[BillMergedDb] 初始化合并账单表 | IdentityId={IdentityId}", identityId);
        EnsureDirectoryExists(identityId);
        var db = GetDb(identityId);
        if (!db.DbMaintenance.IsAnyTable(typeof(BillMerged).FullName))
        {
            LoggingService.Information("[BillMergedDb] 创建合并账单表 | IdentityId={IdentityId}", identityId);
            db.CodeFirst.InitTables(typeof(BillMerged));
        }
        if (!db.DbMaintenance.IsAnyTable(typeof(OperationLog).FullName))
        {
            LoggingService.Information("[BillMergedDb] 创建操作日志表 | IdentityId={IdentityId}", identityId);
            db.CodeFirst.InitTables(typeof(OperationLog));
        }
        LoggingService.Debug("[BillMergedDb] 合并账单表初始化完成 | IdentityId={IdentityId}", identityId);
    }

    private static void EnsureDirectoryExists(int identityId)
    {
        var dir = Path.Combine(BaseDbSource.DataDirectoryPath, "identity");
        if (!Directory.Exists(dir))
        {
            LoggingService.Debug("[BillMergedDb] 创建身份数据库目录 | Path={Path}", dir);
            Directory.CreateDirectory(dir);
        }
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
        LoggingService.Debug("[BillMergedDb] 过滤查询合并账单 | IdentityId={IdentityId} | Page={Page} | Search={Search} | Type={Type}",
            identityId, pageIndex, searchText, classificationType);

        var db = GetDb(identityId);
        var query = db.Queryable<BillMerged>();

        // 时间范围过滤
        if (startTime.HasValue)
        {
            LoggingService.Verbose("[BillMergedDb] 添加时间范围过滤 | Start={Start}", startTime.Value);
            query = query.Where(b => b.Timestamp >= startTime.Value);
        }
        if (endTime.HasValue)
        {
            LoggingService.Verbose("[BillMergedDb] 添加时间范围过滤 | End={End}", endTime.Value);
            query = query.Where(b => b.Timestamp <= endTime.Value);
        }

        // 搜索文本过滤（ItemType / TargetUser / Number）
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.ToLowerInvariant();
            LoggingService.Verbose("[BillMergedDb] 添加搜索过滤 | Search={Search}", search);
            query = query.Where(b =>
                b.ItemType!.Contains(search) ||
                b.TargetUser!.Contains(search) ||
                b.Number!.Contains(search));
        }

        // 分类类型过滤 — 内联表达式，避免 C# 方法无法转译 SQL
        if (!string.IsNullOrWhiteSpace(classificationType))
        {
            LoggingService.Verbose("[BillMergedDb] 添加分类过滤 | Type={Type}", classificationType);
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
        LoggingService.Debug("[BillMergedDb] 查询总数 | TotalCount={Count}", count);

        // 分页 + 排序
        var bills = query
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);

        LoggingService.Debug("[BillMergedDb] 过滤查询完成 | Count={Count} | Total={Total}", bills.Count, count);
        return (bills, count);
    }

    /// <summary>
    /// 查询所有合并账单（分页，无过滤）
    /// </summary>
    public static List<BillMerged> GetAll(int identityId, int pageIndex = 1, int pageSize = 50)
    {
        LoggingService.Debug("[BillMergedDb] 分页查询合并账单 | IdentityId={IdentityId} | Page={Page}", identityId, pageIndex);
        var db = GetDb(identityId);
        var result = db.Queryable<BillMerged>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToPageList(pageIndex, pageSize);
        LoggingService.Debug("[BillMergedDb] 合并账单查询完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 查询所有合并账单（不分页，无过滤）
    /// </summary>
    public static List<BillMerged> GetAll(int identityId)
    {
        LoggingService.Information("[BillMergedDb] 查询所有合并账单 | IdentityId={IdentityId}", identityId);
        var db = GetDb(identityId);
        var result = db.Queryable<BillMerged>()
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
        LoggingService.Information("[BillMergedDb] 合并账单查询完成 | Count={Count}", result.Count);
        return result;
    }

    public static List<BillMerged> GetByTimeRange(int identityId, long startTime, long endTime)
    {
        LoggingService.Debug("[BillMergedDb] 时间范围查询 | IdentityId={IdentityId} | Start={Start} | End={End}",
            identityId, startTime, endTime);
        var db = GetDb(identityId);
        var result = db.Queryable<BillMerged>()
            .Where(b => b.Timestamp >= startTime && b.Timestamp <= endTime)
            .OrderBy(b => b.Timestamp, OrderByType.Desc)
            .ToList();
        LoggingService.Debug("[BillMergedDb] 时间范围查询完成 | Count={Count}", result.Count);
        return result;
    }

    public static BillMerged? GetById(int identityId, int id)
    {
        LoggingService.Verbose("[BillMergedDb] 根据ID查询 | IdentityId={IdentityId} | Id={Id}", identityId, id);
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>().Where(b => b.Id == id).First();
    }

    public static bool ContainsByNumberList(int identityId, string numberListJson)
    {
        LoggingService.Verbose("[BillMergedDb] 检查交易号列表是否存在 | IdentityId={IdentityId}", identityId);
        var db = GetDb(identityId);
        return db.Queryable<BillMerged>().Where(b => b.NumberList == numberListJson).Any();
    }

    public static int Append(int identityId, BillMerged bill)
    {
        LoggingService.Debug("[BillMergedDb] 追加合并账单 | IdentityId={IdentityId} | Number={Number}",
            identityId, bill.Number);
        var db = GetDb(identityId);
        bill.SyncedAt = DateTime.Now.ToString("o");
        var result = db.Insertable(bill).ExecuteReturnIdentity();
        LoggingService.Debug("[BillMergedDb] 合并账单追加成功 | Id={Id}", result);
        return result;
    }

    public static void AppendRange(int identityId, List<BillMerged> bills)
    {
        if (bills.Count == 0)
        {
            LoggingService.Verbose("[BillMergedDb] 批量追加为空，跳过");
            return;
        }

        LoggingService.Information("[BillMergedDb] 批量追加合并账单 | IdentityId={IdentityId} | Count={Count}",
            identityId, bills.Count);
        var db = GetDb(identityId);
        var now = DateTime.Now.ToString("o");
        foreach (var bill in bills) bill.SyncedAt = now;
        db.Insertable(bills).ExecuteCommand();
        LoggingService.Information("[BillMergedDb] 批量追加完成 | Count={Count}", bills.Count);
    }

    public static int AddManual(int identityId, BillMerged bill)
    {
        LoggingService.Information("[BillMergedDb] 手动添加合并账单 | IdentityId={IdentityId} | Type={Type} | Money={Money}",
            identityId, bill.ItemType, bill.MoneyStr);

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
            LoggingService.Information("[BillMergedDb] 手动添加完成 | Id={Id}", id);
            return id;
        }
        catch (Exception ex)
        {
            db.Ado.RollbackTran();
            LoggingService.Error(ex, "[BillMergedDb] 手动添加失败，已回滚");
            throw;
        }
    }

    public static bool DeleteManual(int identityId, int id)
    {
        LoggingService.Warning("[BillMergedDb] 手动删除合并账单 | IdentityId={IdentityId} | Id={Id}", identityId, id);

        var db = GetDb(identityId);
        var bill = GetById(identityId, id);
        if (bill == null)
        {
            LoggingService.Warning("[BillMergedDb] 要删除的账单不存在 | Id={Id}", id);
            return false;
        }

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
            LoggingService.Information("[BillMergedDb] 手动删除完成 | Id={Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            db.Ado.RollbackTran();
            LoggingService.Error(ex, "[BillMergedDb] 手动删除失败，已回滚");
            throw;
        }
    }

    public static bool Update(int identityId, BillMerged bill)
    {
        LoggingService.Information("[BillMergedDb] 更新合并账单 | IdentityId={IdentityId} | Id={Id}", identityId, bill.Id);
        var db = GetDb(identityId);
        var result = db.Updateable(bill).ExecuteCommand() > 0;
        LoggingService.Information("[BillMergedDb] 更新完成 | Success={Success}", result);
        return result;
    }

    public static int GetCount(int identityId)
    {
        LoggingService.Verbose("[BillMergedDb] 获取合并账单总数 | IdentityId={IdentityId}", identityId);
        var db = GetDb(identityId);
        var result = db.Queryable<BillMerged>().Count();
        LoggingService.Debug("[BillMergedDb] 合并账单总数 | Count={Count}", result);
        return result;
    }

    public static (double totalExpense, double totalIncome, int count) GetSummary(
        int identityId, long? startTime = null, long? endTime = null)
    {
        LoggingService.Debug("[BillMergedDb] 获取账单汇总 | IdentityId={IdentityId} | Start={Start} | End={End}",
            identityId, startTime, endTime);

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

        LoggingService.Information("[BillMergedDb] 账单汇总完成 | Expense={Expense} | Income={Income} | Count={Count}",
            totalExpense, totalIncome, bills.Count);

        return (totalExpense, totalIncome, bills.Count);
    }

    public static BillMerged FromBillItemInfo(
        shmtu.datatype.bill.BillItemInfo item,
        string sourceAccountId,
        string? building = null,
        string? room = null)
    {
        LoggingService.Verbose("[BillMergedDb] 转换 BillItemInfo 到 BillMerged | AccountId={AccountId} | Number={Number}",
            sourceAccountId, item.Number);

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
            Building = building,
            Room = room,
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
        LoggingService.Verbose("[BillMergedDb] 转换 BillOriginal 到 BillMerged | Number={Number}", original.Number);

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
            Building = original.Building,
            Room = original.Room,
            MoneyStr = original.MoneyStr,
            Money = original.Money,
            Method = original.Method,
            StatusStr = original.StatusStr,
            IsCombined = original.IsCombined,
            SourceAccountId = original.AccountId,
            IsManual = false,
        };
    }

    /// <summary>
    /// 更新账单备注
    /// </summary>
    public static bool UpdateNotes(int identityId, int billId, string notes)
    {
        LoggingService.Information("[BillMergedDb] 更新账单备注 | IdentityId={IdentityId} | Id={Id}", identityId, billId);
        var db = GetDb(identityId);
        var bill = db.Queryable<BillMerged>().Where(b => b.Id == billId).First();
        if (bill == null)
        {
            LoggingService.Warning("[BillMergedDb] 要更新备注的账单不存在 | Id={Id}", billId);
            return false;
        }
        bill.Notes = notes;
        var result = db.Updateable(bill).UpdateColumns(b => b.Notes).ExecuteCommand() > 0;
        LoggingService.Information("[BillMergedDb] 备注更新完成 | Success={Success}", result);
        return result;
    }

    /// <summary>
    /// 合并多条账单到主账单
    /// 将被合并账单标记为已合并，并将交易号列表追加到主账单
    /// </summary>
    public static bool MergeBills(int identityId, int primaryId, List<int> selectedIds, string? mergeNote = null)
    {
        LoggingService.Information("[BillMergedDb] 合并账单 | IdentityId={IdentityId} | PrimaryId={PrimaryId} | Count={Count}",
            identityId, primaryId, selectedIds.Count);

        var db = GetDb(identityId);

        db.Ado.BeginTran();
        try
        {
            var primary = db.Queryable<BillMerged>().Where(b => b.Id == primaryId).First();
            if (primary == null)
            {
                LoggingService.Warning("[BillMergedDb] 主账单不存在 | Id={Id}", primaryId);
                db.Ado.RollbackTran();
                return false;
            }

            // Collect all numbers from selected bills
            var allNumbers = new List<string>();
            if (!string.IsNullOrEmpty(primary.NumberList))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<List<string>>(primary.NumberList);
                    if (existing != null) allNumbers.AddRange(existing);
                }
                catch { /* ignore parse errors */ }
            }
            if (!string.IsNullOrEmpty(primary.Number))
            {
                if (!allNumbers.Contains(primary.Number)) allNumbers.Add(primary.Number);
            }

            double totalMoney = primary.Money ?? 0;

            foreach (var sid in selectedIds)
            {
                var bill = db.Queryable<BillMerged>().Where(b => b.Id == sid).First();
                if (bill == null) continue;

                // Collect numbers
                if (!string.IsNullOrEmpty(bill.NumberList))
                {
                    try
                    {
                        var nums = JsonSerializer.Deserialize<List<string>>(bill.NumberList);
                        if (nums != null) allNumbers.AddRange(nums.Where(n => !allNumbers.Contains(n)));
                    }
                    catch { /* ignore */ }
                }
                if (!string.IsNullOrEmpty(bill.Number) && !allNumbers.Contains(bill.Number))
                {
                    allNumbers.Add(bill.Number);
                }

                // Accumulate money
                totalMoney += bill.Money ?? 0;

                // Mark as combined and delete
                bill.IsCombined = true;
                db.Updateable(bill).UpdateColumns(b => b.IsCombined).ExecuteCommand();
                db.Deleteable<BillMerged>().Where(b => b.Id == sid).ExecuteCommand();
            }

            // Update primary bill
            primary.NumberList = JsonSerializer.Serialize(allNumbers);
            primary.Money = totalMoney;
            primary.MoneyStr = $"{totalMoney:F2}";
            primary.IsCombined = true;
            if (!string.IsNullOrEmpty(mergeNote))
                primary.Notes = string.IsNullOrEmpty(primary.Notes) ? mergeNote : $"{primary.Notes}; {mergeNote}";

            db.Updateable(primary)
                .UpdateColumns(b => new { b.NumberList, b.Money, b.MoneyStr, b.IsCombined, b.Notes })
                .ExecuteCommand();

            // Log the operation
            db.Insertable(new OperationLog
            {
                OperationType = "merge",
                RecordNumbers = primary.NumberList,
                OperationTime = DateTime.Now.ToString("o"),
                Description = $"合并 {selectedIds.Count} 条账单到主账单 #{primaryId}",
                AccountId = primary.SourceAccountId,
            }).ExecuteCommand();

            db.Ado.CommitTran();
            LoggingService.Information("[BillMergedDb] 合并账单完成 | PrimaryId={PrimaryId}", primaryId);
            return true;
        }
        catch (Exception ex)
        {
            db.Ado.RollbackTran();
            LoggingService.Error(ex, "[BillMergedDb] 合并账单失败，已回滚");
            throw;
        }
    }

    #region 分类统计方法

    /// <summary>
    /// 按消费类型统计金额 — 使用 BillClassifier 进行分类后聚合
    /// 注意：结果在内存中计算，适用于中等数据量
    /// </summary>
    public static Dictionary<string, double> GetSummaryByType(
        int identityId,
        shmtu.classifier.BillClassifier classifier,
        long? startTime = null,
        long? endTime = null)
    {
        LoggingService.Debug("[BillMergedDb] 按类型统计金额 | IdentityId={IdentityId} | Start={Start} | End={End}",
            identityId, startTime, endTime);

        var db = GetDb(identityId);
        var query = db.Queryable<BillMerged>();

        if (startTime.HasValue)
            query = query.Where(b => b.Timestamp >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(b => b.Timestamp <= endTime.Value);

        var bills = query.ToList();
        var result = new Dictionary<string, double>();

        foreach (var bill in bills)
        {
            var category = classifier.Classify(bill.ItemType ?? "", bill.TargetUser ?? "");
            var typeName = category.DisplayName();
            var money = bill.Money ?? 0;
            result.TryGetValue(typeName, out var current);
            result[typeName] = current + money;
        }

        LoggingService.Information("[BillMergedDb] 按类型统计完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 按楼栋统计金额 — 使用数据库中存储的 Building 字段聚合
    /// </summary>
    public static Dictionary<string, double> GetSummaryByBuilding(
        int identityId,
        long? startTime = null,
        long? endTime = null)
    {
        LoggingService.Debug("[BillMergedDb] 按楼栋统计金额 | IdentityId={IdentityId} | Start={Start} | End={End}",
            identityId, startTime, endTime);

        var db = GetDb(identityId);
        var query = db.Queryable<BillMerged>();

        if (startTime.HasValue)
            query = query.Where(b => b.Timestamp >= startTime.Value);
        if (endTime.HasValue)
            query = query.Where(b => b.Timestamp <= endTime.Value);

        var bills = query.ToList();
        var result = new Dictionary<string, double>();

        foreach (var bill in bills)
        {
            var building = bill.Building ?? "未知";
            var money = bill.Money ?? 0;
            result.TryGetValue(building, out var current);
            result[building] = current + money;
        }

        LoggingService.Information("[BillMergedDb] 按楼栋统计完成 | Count={Count}", result.Count);
        return result;
    }

    #endregion
}
