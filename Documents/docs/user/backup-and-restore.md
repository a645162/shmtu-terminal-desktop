# 怎么备份和恢复

应用的所有数据都存储在本地，包括账号、加密凭证、账单和快照。**强烈建议定期备份**，尤其是重装系统前。

## 备份的内容

应用目录下的 `Data/` 子目录保存了所有持久化数据：

```
~/.local/share/shmtu-terminal-desktop/Data/   (Linux)
%APPDATA%\shmtu-terminal-desktop\Data\         (Windows)
~/Library/Application Support/shmtu-terminal-desktop/Data/   (macOS)
```

| 文件/目录 | 内容 | 重要程度 |
|---|---|---|
| `config.toml` | 全局配置（主题、字体、同步模式等） | 中 |
| `identities.enc` | 加密的身份和账号凭证 | **高** |
| `bills.db` | 同步下来的所有账单 (Sqlite) | **高** |
| `snapshots/` | 手动/自动创建的快照 | **高** |
| `logs/` | 运行日志，可清理 | 低 |
| `cache/` | UI 缓存、临时文件 | 低 |

> `identities.enc` 用启动密码派生密钥加密，**改密码或丢失密码会导致所有账号不可解密**，但账单数据库 `bills.db` 仍是明文可读。

## 备份方式

### 方式 1：应用内快照（推荐）

进入 **设置 → 数据 → 快照** → **创建快照**。快照会保存当前 `bills.db` 的完整副本到 `snapshots/<时间戳>/` 目录。

恢复时选择某个快照 → **恢复**。系统会**替换**当前数据库，所以建议恢复前先做一次新快照。

### 方式 2：手动复制

直接压缩 `Data/` 整个目录到外部存储：

```bash
# Linux
tar czf shmtu-backup-$(date +%F).tar.gz ~/.local/share/shmtu-terminal-desktop/Data/

# Windows (PowerShell)
Compress-Archive "$env:APPDATA\shmtu-terminal-desktop\Data" shmtu-backup.zip
```

### 方式 3：导出账单 CSV/JSON

如果只想备份**账单数据**（不含账号），进入 **设置 → 导出** → 选择格式：

- **CSV**：Excel 可直接打开
- **JSON**：保留所有字段，便于二次处理
- **钱迹 CSV**：适配"钱迹"记账 App 的导入格式

## 恢复流程

### 从快照恢复

1. 应用内 **设置 → 数据 → 快照**
2. 选择目标快照
3. 点击 **恢复** → 确认
4. 应用重启，数据库回到快照时刻

### 从手动备份恢复

1. 关闭应用
2. 用备份的 `Data/` 目录覆盖当前目录
3. 重启应用

> 恢复后**首次同步**建议改为**增量同步**，避免覆盖恢复的数据。

## 自动化建议

- **每周一次**：在系统层面用 `cron` / 任务计划程序 复制 `Data/` 到外部存储
- **每次大版本更新前**：手动做一次快照
- **导出到云盘**：将 `Data/` 同步到 OneDrive/iCloud/坚果云（注意加密）

## 下一步

- [常见问题](/user/faq) — 数据恢复失败时怎么办
