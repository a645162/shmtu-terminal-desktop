using System.IO;
using System.Text;
using System.Text.Json;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Services.BillClassification;

namespace shmtu.terminal.desktop.Services.Export;

/// <summary>
/// 导出格式
/// </summary>
public enum ExportFormat
{
    Csv,
    Json,
    Qianji,
}

/// <summary>
/// 账单数据导出服务 — 支持 CSV/JSON/钱迹格式导出
/// </summary>
public static class BillExportService
{
    /// <summary>
    /// 导出身份合并数据
    /// </summary>
    public static async Task ExportAsync(
        int identityId,
        string identityName,
        ExportFormat format,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var bills = BillMergedDb.GetAll(identityId);

        Task exportTask = format switch
        {
            ExportFormat.Csv => ExportCsvAsync(bills, filePath, cancellationToken),
            ExportFormat.Json => ExportJsonAsync(bills, identityName, filePath, cancellationToken),
            ExportFormat.Qianji => ExportQianjiAsync(bills, identityName, filePath, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        await exportTask;
    }

    /// <summary>
    /// 导出指定时间范围的数据
    /// </summary>
    public static async Task ExportAsync(
        int identityId,
        string identityName,
        ExportFormat format,
        string filePath,
        long startTime,
        long endTime,
        CancellationToken cancellationToken = default)
    {
        var bills = BillMergedDb.GetByTimeRange(identityId, startTime, endTime);

        Task exportTask = format switch
        {
            ExportFormat.Csv => ExportCsvAsync(bills, filePath, cancellationToken),
            ExportFormat.Json => ExportJsonAsync(bills, identityName, filePath, cancellationToken),
            ExportFormat.Qianji => ExportQianjiAsync(bills, identityName, filePath, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        await exportTask;
    }

    /// <summary>
    /// 导出指定账号的原始数据
    /// </summary>
    public static async Task ExportAccountOriginalAsync(
        string accountId,
        ExportFormat format,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var bills = BillOriginalDb.GetAll(accountId);

        // 转换为 BillMerged 格式再导出
        var mergedBills = bills.Select(BillMergedDb.FromBillOriginal).ToList();

        Task exportTask = format switch
        {
            ExportFormat.Csv => ExportCsvAsync(mergedBills, filePath, cancellationToken),
            ExportFormat.Json => ExportJsonAsync(mergedBills, $"账号原始数据 - {accountId}", filePath, cancellationToken),
            ExportFormat.Qianji => ExportQianjiAsync(mergedBills, $"账号原始数据 - {accountId}", filePath, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        await exportTask;
    }

    #region CSV 导出

    /// <summary>
    /// CSV 格式导出 — UTF-8 BOM 编码，Excel 兼容
    /// </summary>
    private static async Task ExportCsvAsync(
        List<BillMerged> bills,
        string filePath,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // 表头
        sb.AppendLine("日期时间,交易名称,交易号,对方账户,金额,支付方式,状态");

        foreach (var bill in bills)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine($"{bill.DateTimeFormatted},{bill.ItemType},{bill.Number},{bill.TargetUser},{bill.MoneyStr},{bill.Method},{bill.StatusStr}");
        }

        // UTF-8 with BOM
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = Encoding.UTF8.GetBytes(sb.ToString());

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await fs.WriteAsync(bom, cancellationToken);
        await fs.WriteAsync(content, cancellationToken);
    }

    #endregion

    #region JSON 导出

    /// <summary>
    /// JSON 格式导出 — 包含分类信息，支持重新导入
    /// </summary>
    private static async Task ExportJsonAsync(
        List<BillMerged> bills,
        string identityName,
        string filePath,
        CancellationToken cancellationToken)
    {
        var classifier = new BillClassifier();

        var exportData = new
        {
            export_time = DateTime.Now.ToString("o"),
            identity_name = identityName,
            source = "merged",
            bills = bills.Select(b =>
            {
                var classification = classifier.Classify(b);
                return new
                {
                    date_str = b.DateStr,
                    time_str = b.TimeStr,
                    date_time_formatted = b.DateTimeFormatted,
                    end_date_time_formatted = b.EndDateTimeFormatted,
                    timestamp = b.Timestamp,
                    end_timestamp = b.EndTimestamp,
                    item_type = b.ItemType,
                    number = b.Number,
                    number_list = b.NumberList != null
                        ? JsonSerializer.Deserialize<List<string>>(b.NumberList)
                        : [],
                    target_user = b.TargetUser,
                    money_str = b.MoneyStr,
                    money = b.Money,
                    method = b.Method,
                    status_str = b.StatusStr,
                    is_combined = b.IsCombined,
                    source_account_id = b.SourceAccountId,
                    is_manual = b.IsManual,
                    classification = new
                    {
                        type = classification.Type,
                        building = classification.Building,
                        room = classification.Room,
                        meal = classification.Meal,
                    },
                };
            }).ToList(),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var json = JsonSerializer.Serialize(exportData, options);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
    }

    #endregion

    #region 钱迹格式导出

    /// <summary>
    /// 钱迹 App 导入格式导出
    /// </summary>
    private static async Task ExportQianjiAsync(
        List<BillMerged> bills,
        string identityName,
        string filePath,
        CancellationToken cancellationToken)
    {
        var classifier = new BillClassifier();

        var qianjiData = bills.Select(b =>
        {
            var classification = classifier.Classify(b);
            var money = b.Money ?? 0;

            return new
            {
                type = money >= 0 ? 1 : 0, // 0=支出，1=收入
                money = Math.Abs(money),
                category = MapToQianjiCategory(classification.Type),
                account = b.Method ?? "校园卡",
                remark = BuildQianjiRemark(classification),
                time = b.Timestamp ?? 0,
            };
        }).ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var json = JsonSerializer.Serialize(qianjiData, options);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// 映射为钱迹分类体系
    /// </summary>
    private static string MapToQianjiCategory(string type)
    {
        return type switch
        {
            "充值" => "收入",
            "电费" => "居家",
            "洗澡" => "日常",
            "热水" => "日常",
            "蛋糕" => "餐饮",
            "食堂" => "餐饮",
            _ => "其他",
        };
    }

    /// <summary>
    /// 构建钱迹备注
    /// </summary>
    private static string BuildQianjiRemark(ClassificationResult classification)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(classification.Room))
            parts.Add(classification.Room);
        if (!string.IsNullOrEmpty(classification.Meal) && classification.Meal != "非用餐时段")
            parts.Add(classification.Meal);
        return parts.Count > 0 ? string.Join("-", parts) : classification.OriginalTargetUser ?? "";
    }

    #endregion
}
