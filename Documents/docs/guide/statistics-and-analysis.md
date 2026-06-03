# 统计分析

应用提供多维度的账单分析，从"今日花了多少"到"半年消费趋势"。本章说明每个统计项的口径和解读方法。

## 统计入口

- **首页 Tab**：4 张摘要卡片（粗粒度、实时）
- **统计 Tab**：4 类详细图表（细粒度、可交互）
- **账单 Tab**：按筛选条件临时聚合

## 首页摘要卡片

| 卡片 | 含义 | SQL 大致 |
|---|---|---|
| 今日消费 | 服务器时区今天 00:00 之后的所有消费 | `SUM(amount) WHERE server_time >= today AND type=Consume` |
| 本月消费 | 自然月 1 号到现在的消费 | `SUM(amount) WHERE server_time >= month_start AND type=Consume` |
| 账户余额 | 最后一次同步时服务器返回的余额 | `balance FROM bills WHERE balance IS NOT NULL ORDER BY server_time DESC LIMIT 1` |
| 本月笔数 | 自然月消费次数 | `COUNT(*) WHERE server_time >= month_start AND type=Consume` |

## 统计 Tab

### 分类占比

**图表类型**：环形饼图（LiveCharts2 PieChart）
**口径**：所选时间范围内，各分类的消费金额占比
**交互**：

- 鼠标悬停显示：分类名、金额、占比
- 点击扇区：下钻到该分类下的商户列表
- 双击空白处：返回上一级

**典型用法**：查看本月"钱主要花在哪"。

### 趋势图

**图表类型**：折线图（CartesianChart + LineSeries）
**口径**：所选时间范围内，每天的消费金额
**X 轴**：日期
**Y 轴**：金额
**可叠加**：日均线、本月平均、昨日对比

**典型用法**：观察消费高峰（周末？月初？）。

### 商户排行

**图表类型**：横向柱状图（RowSeries）
**口径**：所选时间范围内，金额 Top 10 商户（`position` 字段去重）
**交互**：点击柱条下钻到该商户的所有交易

**典型用法**：识别高频小额消费场所。

### 月度对比

**图表类型**：柱状图（ColumnSeries）
**口径**：最近 6 个月的月度总消费
**X 轴**：月份（YYYY-MM）
**Y 轴**：金额
**可叠加**：分类堆叠（看每月各类占比变化）

**典型用法**：观察学期/月度消费习惯变化。

## 统计口径

### 时间归属

所有时间维度都基于 `server_time`（**服务器返回的交易时间**），不是刷卡时间也不是同步时间。原因：

- 服务器时间是权威记录
- 跨设备刷卡不影响归属
- 凌晨 0:00-6:00 的交易按服务器日期归属（可能与本地日期不一致）

### 充值与退款

- **充值/转入**：归为 `Deposit` 分类，**不计入消费总额**
- **退款**：`amount` 为负数，统计时按 `type=Recharge` 处理

> 如需"净消费"，可在导出 CSV 后用 Excel `SUMIF` 自定义。

### 已退账单

标记为 `status=Refunded` 的账单：

- 从分类占比中**剔除**
- 从趋势图中**剔除**
- 从商户排行中**剔除**
- 仍保留在账单 Tab 中，可筛选查看

### 账户余额

余额来自最后一次同步时服务器返回的最新值。**不会**：

- 自动刷新（避免不必要的请求）
- 累加计算（以服务器为准）

余额显示为 `---` 表示最近一次同步未拿到余额信息（极少见，多为服务端维护）。

## 时间范围筛选

所有统计 Tab 都支持时间范围切换：

- **预设**：今日 / 本周 / 本月 / 本季 / 本年
- **自定义**：起始日期 + 结束日期

切换时间范围会触发 SQL 重新聚合，首次可能略有卡顿（>100k 条数据）。

## 缓存策略

`StatisticsService` 对 4 类统计各自维护内存缓存：

```csharp
public class StatisticsService : IStatisticsService
{
    private readonly Dictionary<StatsKey, (DateTime Expires, object Value)> _cache = new();
    private TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public async Task<StatsResult> GetCategoryBreakdownAsync(DateRange range)
    {
        var key = new StatsKey(nameof(GetCategoryBreakdownAsync), range);
        if (_cache.TryGetValue(key, out var hit) && hit.Expires > DateTime.UtcNow)
            return (StatsResult)hit.Value;

        var result = await ComputeFromDb(range);
        _cache[key] = (DateTime.UtcNow + _ttl, result);
        return result;
    }
}
```

- 同步完成后 `InvalidateCacheAsync()` 主动失效
- 5 分钟 TTL 防止数据过期太久

## 导出统计

如需把统计图表导入到 PPT 或报告：

1. **截图**：点击统计图表右上角 **📷 导出为 PNG**
2. **CSV**：进入 **账单 Tab → 导出 → CSV**，包含原始数据
3. **JSON**：同上，保留所有字段

## 性能

- 1 万笔账单：分类/趋势/排行 < 100ms
- 10 万笔账单：分类/趋势 < 500ms，月度对比 < 1s
- 100 万笔账单：建议建立分区或冷热数据分离（暂未实现）

> 如果统计加载明显卡顿，尝试 **设置 → 数据 → 重建索引**。

## 下一步

- [设置与数据](/guide/settings-and-data) — 调整行为与数据管理
- [FAQ](/guide/faq) — 常见统计问题
