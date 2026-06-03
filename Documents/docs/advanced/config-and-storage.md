# 配置与存储

本节讲解应用的配置文件结构、Sqlite 数据库 schema、加密策略与快照机制。

## 配置文件

应用使用 **TOML** 格式（`Tomlyn` 解析），文件位于：

```
<Data>/config.toml
```

> 配置文件为**明文**，不要把密码写在里面。`identities.enc` 是单独加密的凭证文件。

### 结构示例

```toml
[app]
version = "1.0.0"
theme = "system"           # "light" | "dark" | "system"
font_size = "medium"       # "small" | "medium" | "large" | "xlarge"
language = "zh-CN"

[sync]
default_mode = "incremental"   # "incremental" | "full"
captcha_mode = "remote"        # "manual" | "remote" | "local"
early_stop_threshold = 5
max_pages = 100
concurrent_accounts = 1        # 多账号并发

[captcha]
remote_url = "http://127.0.0.1:5000"
timeout_seconds = 30
retry_count = 3

[ocr]
model_dir = "ocr/models"
prefer_gpu = false             # DirectML EP

[ui]
show_welcome = true
auto_refresh_seconds = 300
chart_animation = true

[logging]
min_level = "Information"      # "Verbose" | "Debug" | "Information" | "Warning" | "Error"
file_rotation = "daily"        # "daily" | "size"
retention_days = 30
```

## 配置服务

`Services/Config/TomlConfigService.cs` 实现了：

- **强类型访问**：`config.Sync.CaptchaMode` 而不是 `config["sync"]["captcha_mode"]`
- **热加载**：文件被外部修改时自动重载（`FileSystemWatcher`）
- **自动保存**：脏值检测 + 延迟写盘（5s 防抖）
- **迁移**：版本升级时自动迁移旧字段

```csharp
public class TomlConfigService : ITomlConfigService
{
    public AppConfig Config { get; private set; }
    public event EventHandler<AppConfig>? ConfigChanged;

    public void Update(Action<AppConfig> mutator)
    {
        mutator(Config);
        _pendingSave = true;
        ConfigChanged?.Invoke(this, Config);
    }
}
```

## 数据库

应用使用 **Sqlite + SqlSugarCore 5.1.4**。

### 关键表

```sql
-- 账单
CREATE TABLE bills (
    id            INTEGER PRIMARY KEY,
    number        TEXT UNIQUE NOT NULL,    -- 交易号
    account_id    TEXT NOT NULL,
    amount        REAL NOT NULL,           -- 元
    balance       REAL,                    -- 交易后余额
    type          INTEGER NOT NULL,        -- BillType 枚举
    category      INTEGER,                 -- BillCategory 枚举
    position      TEXT,                    -- 商户/位置
    description   TEXT,
    status        INTEGER NOT NULL,        -- 正常/已退
    server_time   TEXT NOT NULL,           -- ISO 8601
    local_time    TEXT NOT NULL,
    raw_html      TEXT                     -- 原始 HTML（可清理）
);
CREATE INDEX idx_bills_account_time ON bills(account_id, server_time);

-- 同步状态
CREATE TABLE sync_state (
    account_id     TEXT PRIMARY KEY,
    last_sync_at   TEXT,
    last_page      INTEGER,
    last_number    TEXT,                   -- 早停参考点
    total_synced   INTEGER DEFAULT 0
);

-- 快照元数据
CREATE TABLE snapshots (
    id          INTEGER PRIMARY KEY,
    name        TEXT NOT NULL,
    created_at  TEXT NOT NULL,
    file_path   TEXT NOT NULL,
    size_bytes  INTEGER,
    note        TEXT
);

-- 设置/键值存储
CREATE TABLE kv (
    k TEXT PRIMARY KEY,
    v TEXT NOT NULL
);
```

### 迁移

`Database/Migrations/` 目录下每个迁移是一个版本号文件：

```csharp
[Migration(2025_06_01_001)]
public class AddBillRawHtml : Migration
{
    public override void Up()
    {
        Alter.Column("raw_html").OnTable("bills").AsString().Nullable();
    }
}
```

启动时 `Migrate` 自动按版本顺序执行。

## 加密

`Services/Security/EncryptionService.cs` 负责：

### 启动密码 → 密钥

```
启动密码 (string)
   ↓ PBKDF2-SHA256 (100,000 轮, salt=16B)
MasterKey (32B)
   ↓ HKDF-SHA256 (info="identities")
IdentitiesKey (32B)
```

### 数据加密

```csharp
// AES-GCM
public byte[] Encrypt(byte[] plaintext, byte[] key)
{
    var nonce = RandomNumberGenerator.GetBytes(12);
    var ciphertext = new byte[plaintext.Length];
    var tag = new byte[16];

    using var aes = new AesGcm(key);
    aes.Encrypt(nonce, plaintext, ciphertext, tag);

    return [nonce, ..tag, ..ciphertext];
}
```

### 加密的内容

| 文件 | 加密算法 | Key 派生 |
|---|---|---|
| `identities.enc` | AES-GCM-256 | 启动密码 + HKDF |
| `config.toml` | **不加密** | - |
| `bills.db` | **不加密** | - |

> 故意让 `bills.db` 保持明文 — 用户即使忘记密码也能查看历史账单。

## 快照

`Services/Export/DataSnapshotService.cs`：

```csharp
public async Task<Snapshot> CreateAsync(string name, string? note = null)
{
    var snapshotDir = Path.Combine(_dataDir, "snapshots", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
    Directory.CreateDirectory(snapshotDir);

    // 1. 拷贝 bills.db
    File.Copy(
        Path.Combine(_dataDir, "bills.db"),
        Path.Combine(snapshotDir, "bills.db"));

    // 2. 记录元数据
    await _db.Insertable(new Snapshot
    {
        Name = name,
        CreatedAt = DateTime.UtcNow,
        FilePath = snapshotDir,
        Note = note
    }).ExecuteCommandAsync();

    return new Snapshot { ... };
}
```

恢复时反向操作 — 用快照目录的 `bills.db` 替换当前文件，然后应用重启。

## 下一步

阅读 [构建与发布](/advanced/build-and-release) 了解打包和 CI 流程。
