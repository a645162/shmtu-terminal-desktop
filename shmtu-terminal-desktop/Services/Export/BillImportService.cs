using System.IO;
using System.Text;
using System.Text.Json;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Models.Bill;

namespace shmtu.terminal.desktop.Services.Export;

/// <summary>
/// 数据导入服务 — 从 JSON/CSV 文件导入账单数据
/// </summary>
public static class BillImportService
{
    /// <summary>
    /// 导入结果
    /// </summary>
    public class ImportResult
    {
        public int TotalCount { get; set; }
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = [];
    }

    /// <summary>
    /// JSON 格式导入 — 导入到指定身份的合并数据表
    /// </summary>
    public static async Task<ImportResult> ImportJsonAsync(
        int identityId,
        string filePath,
        bool skipExisting = true,
        long? startTimeFilter = null,
        long? endTimeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult();

        try
        {
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
            var jsonDoc = JsonDocument.Parse(json);

            var billsArray = jsonDoc.RootElement.TryGetProperty("bills", out var billsProp)
                ? billsProp
                : jsonDoc.RootElement;

            InitDb.InitIdentityDb(identityId);

            foreach (var billElement in billsArray.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.TotalCount++;

                try
                {
                    var bill = ParseBillMerged(billElement);

                    // 时间范围过滤
                    if (startTimeFilter.HasValue && bill.Timestamp < startTimeFilter)
                    {
                        result.SkippedCount++;
                        continue;
                    }
                    if (endTimeFilter.HasValue && bill.Timestamp > endTimeFilter)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // 去重检查
                    if (skipExisting && bill.NumberList != null &&
                        BillMergedDb.ContainsByNumberList(identityId, bill.NumberList))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // 导入为手动记录
                    bill.IsManual = true;
                    bill.SourceAccountId = "import";
                    BillMergedDb.Append(identityId, bill);
                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"第 {result.TotalCount} 条记录导入失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"读取文件失败: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// CSV 格式导入
    /// </summary>
    public static async Task<ImportResult> ImportCsvAsync(
        int identityId,
        string filePath,
        bool skipExisting = true,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportResult();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, cancellationToken);

            // 跳过表头
            for (var i = 1; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.TotalCount++;

                try
                {
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < 7)
                    {
                        result.Errors.Add($"第 {i + 1} 行字段不足");
                        continue;
                    }

                    var bill = new BillMerged
                    {
                        DateTimeFormatted = fields[0],
                        ItemType = fields[1],
                        Number = fields[2],
                        TargetUser = fields[3],
                        MoneyStr = fields[4],
                        Method = fields[5],
                        StatusStr = fields[6],
                        IsManual = true,
                        SourceAccountId = "import",
                    };

                    // 解析时间戳
                    if (DateTime.TryParse(fields[0], out var dt))
                    {
                        bill.Timestamp = ((DateTimeOffset)dt).ToUnixTimeSeconds();
                    }

                    // 解析金额
                    if (double.TryParse(fields[4], out var money))
                    {
                        bill.Money = money;
                    }

                    bill.NumberList = JsonSerializer.Serialize(new List<string> { bill.Number });

                    // 去重
                    if (skipExisting && bill.NumberList != null &&
                        BillMergedDb.ContainsByNumberList(identityId, bill.NumberList))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    BillMergedDb.Append(identityId, bill);
                    result.ImportedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"第 {i + 1} 行导入失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"读取文件失败: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 解析 JSON 元素为 BillMerged
    /// </summary>
    private static BillMerged ParseBillMerged(JsonElement element)
    {
        var bill = new BillMerged
        {
            DateStr = GetStringProperty(element, "date_str", ""),
            TimeStr = GetStringProperty(element, "time_str", ""),
            DateTimeFormatted = GetStringProperty(element, "date_time_formatted", ""),
            EndDateTimeFormatted = GetStringProperty(element, "end_date_time_formatted", ""),
            Timestamp = GetLongProperty(element, "timestamp"),
            EndTimestamp = GetLongProperty(element, "end_timestamp"),
            ItemType = GetStringProperty(element, "item_type", ""),
            Number = GetStringProperty(element, "number", ""),
            TargetUser = GetStringProperty(element, "target_user", ""),
            MoneyStr = GetStringProperty(element, "money_str", "0"),
            Money = GetDoubleProperty(element, "money"),
            Method = GetStringProperty(element, "method", ""),
            StatusStr = GetStringProperty(element, "status_str", ""),
            IsCombined = GetBoolProperty(element, "is_combined"),
            SourceAccountId = GetStringProperty(element, "source_account_id", ""),
            IsManual = GetBoolProperty(element, "is_manual"),
        };

        // 解析 number_list
        if (element.TryGetProperty("number_list", out var nlProp) && nlProp.ValueKind == JsonValueKind.Array)
        {
            var numberList = nlProp.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
            bill.NumberList = JsonSerializer.Serialize(numberList);
        }
        else
        {
            bill.NumberList = JsonSerializer.Serialize(new List<string> { bill.Number });
        }

        return bill;
    }

    private static string GetStringProperty(JsonElement element, string propertyName, string defaultValue)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetString() ?? defaultValue : defaultValue;
    }

    private static long? GetLongProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt64()
            : null;
    }

    private static double? GetDoubleProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetDouble()
            : null;
    }

    private static bool GetBoolProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// 解析 CSV 行（处理引号内的逗号）
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString().Trim());
        return result.ToArray();
    }
}
