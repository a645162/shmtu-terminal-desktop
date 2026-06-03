# 账单查询与同步

本章解释账单的数据结构、查询方式、同步策略与去重/重建机制。

## 账单数据结构

每笔交易（`BillItem`）包含以下字段：

| 字段 | 类型 | 含义 |
|---|---|---|
| `id` | int64 | 数据库主键 |
| `number` | string (unique) | **交易号**（CAS 服务器返回） |
| `account_id` | string | 所属账号 |
| `amount` | decimal | 金额（元，正数=消费，负数=充值/退款） |
| `balance` | decimal | 交易后账户余额 |
| `type` | enum | `All` / `Consume` / `Recharge` |
| `category` | enum | 见 [分类规则](#分类规则) |
| `position` | string | 商户或位置描述 |
| `description` | string | 备注 |
| `status` | enum | `Normal` / `Refunded` |
| `server_time` | ISO 8601 | 服务器交易时间 |
| `local_time` | ISO 8601 | 同步到本地的时间 |
| `raw_html` | string (nullable) | 原始 HTML（默认不存） |

## 查询方式

### 列表查询

进入 **账单 Tab**，支持：

- **时间范围**：今日 / 本周 / 本月 / 自定义
- **金额范围**：最小-最大
- **分类多选**：食堂、超市、热水……
- **商户搜索**：模糊匹配 `position` 字段
- **关键字搜索**：匹配 `number` / `description`
- **状态筛选**：正常 / 已退

### 详情查看

右键某条 → **查看详情**。显示：

- 完整交易信息
- 同步来源（哪个账号、哪次同步）
- 原始 HTML（如果启用）
- 纠正分类的入口

## 同步模式

应用提供 3 种同步模式（与 Tauri 版一致）：

### 增量同步

```
从最后同步点（sync_state 表）开始
   ↓
按页请求，直到连续 5 条已存在 → 早停
```

**适用**：日常使用，速度快（通常 < 5 秒）。

### 全量同步

```
从第 1 页开始
   ↓
按页请求，直到服务器返回最后一页
   ↓
合并去重写入
```

**适用**：数据缺失补齐，或首次安装。**耗时 1-3 分钟**。

### 单账号同步

只同步当前选中账号，跳过其他账号。**适用**：多账号情况下只想更新某一个。

## 早停机制

增量同步的关键优化。`BillSyncOptions.EarlyStopThreshold`（默认 5）控制：

```
翻到第 N 页：
  - 条目 1: 已存在 (count=1)
  - 条目 2: 已存在 (count=2)
  - 条目 3: 新增   (count=0)
  - 条目 4: 新增   (count=0)
  - 条目 5: 已存在 (count=1)
翻到第 N+1 页：
  - 条目 1: 已存在 (count=2)
  - ...
  - 条目 5: 已存在 (count=6)  ← 触发早停
```

**为何是 5？** 太小容易错过遗漏（CAS 偶尔会乱序），太大浪费请求。

## 去重

每笔交易在数据库中以 `number`（交易号）唯一标识。`IBillStore.Contains(number)` 在合并前判断：

```csharp
public interface IBillStore
{
    bool Contains(string number);
    void Merge(List<BillItemInfo> newBills);
}
```

去重保证**幂等** — 同一笔交易多次同步不会重复插入。

## 重建索引

遇到以下情况考虑重建索引：

- 账单列表打开缓慢
- 某些交易"消失"又重现
- 切换电脑后导入旧数据

**设置 → 数据 → 重建索引** 会：

1. 关闭 Sqlite 连接
2. 重建 `idx_bills_account_time` 等索引
3. 重新打开连接

**不会**修改数据。

## 重建数据（慎用）

**设置 → 数据 → 清空并重建** 会：

1. 备份当前 `bills.db` 为 `bills.db.bak.<时间戳>`
2. 删除当前数据库
3. 重建空库
4. 下次同步从第 1 页开始全量拉取

**适用**：数据库严重损坏无法读取。

## 同步冲突处理

理论上同一笔交易可能在多设备同步时产生**几乎相同**但不完全相同的记录（服务器时间略有差异）。应用通过 `number` 主键解决 — 只有第一条写入成功。

如发现重复：

1. **账单 Tab** → 搜索相同金额/时间
2. 右键 → **标记为已退**（如果确实是退款）
3. 或 **删除**（仅当确认是重复）

## 分类规则

账单按 `position` 字段（商户/位置）自动分类，规则在 `shmtu-dotnet-lib/Classifier/PositionTranslator.cs`：

| 分类 | 匹配关键字 |
|---|---|
| 充值 | 充值、转入、Transfer |
| 电费 | 电费、Electricity |
| 洗澡 | 浴室、Bath、Shower |
| 热水 | 热水、HotWater |
| 点心 | 面包、蛋糕、Cake |
| 食堂 | 食堂、食堂一楼、Canteen |
| 图书馆 | 图书馆、Library |
| 校医院 | 医院、医务室、Hospital |
| 超市 | 超市、便利店、Shop |
| 洗衣 | 洗衣、Laundry |
| 网络 | 网费、网络、Network |
| 交通 | 公交、地铁、Transport |
| 其他 | （未匹配） |

> 规则基于关键字的优先级匹配，不依赖固定商户名。发现分类错误时，可在 **账单** 中右键 → **纠正分类** 提交。

## 性能与缓存

- **数据库**：Sqlite WAL 模式，写入不阻塞读取
- **统计缓存**：`StatisticsService` 对今日/本月等指标做 5 分钟内存缓存
- **索引**：`account_id + server_time` 复合索引覆盖 95% 查询

## 下一步

- [统计分析](/guide/statistics-and-analysis) — 看汇总与趋势
- [设置与数据](/guide/settings-and-data) — 自定义行为
