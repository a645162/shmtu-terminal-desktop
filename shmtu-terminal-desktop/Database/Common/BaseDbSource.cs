using shmtu.terminal.desktop.Services;
using System;
using Microsoft.Data.Sqlite;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Common;

/// <summary>
/// Common class for database operations
/// 数据库操作的公共类
/// </summary>
public abstract class BaseDbSource
{
    /// <summary>
    /// 数据目录 — 使用操作系统标准的应用数据目录（对齐 Rust 版的 app_data_dir）。
    /// Linux: ~/.local/share/shmtu-terminal-desktop/
    /// Windows: %APPDATA%/shmtu-terminal-desktop/
    /// macOS: ~/Library/Application Support/shmtu-terminal-desktop/
    /// 如果系统目录不存在但本地 Data/ 存在（旧版数据），自动迁移。
    /// </summary>
    public static readonly string DataDirectoryPath = InitializeDataDirectory();
    private static readonly string DbExtension = "sqlite";

    private static string InitializeDataDirectory()
    {
        var appName = "shmtu-terminal-desktop";
        string systemDataDir;

        if (OperatingSystem.IsLinux())
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            systemDataDir = !string.IsNullOrEmpty(xdg)
                ? Path.Combine(xdg, appName)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", appName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            systemDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", appName);
        }
        else
        {
            systemDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        }

        // 旧版数据迁移：如果系统目录不存在但本地 Data/ 存在
        var legacyDataDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"));
        if (!Directory.Exists(systemDataDir) && Directory.Exists(legacyDataDir))
        {
            try
            {
                Directory.CreateDirectory(systemDataDir);
                foreach (var file in Directory.GetFiles(legacyDataDir))
                {
                    var dest = Path.Combine(systemDataDir, Path.GetFileName(file));
                    File.Move(file, dest);
                }
                foreach (var dir in Directory.GetDirectories(legacyDataDir))
                {
                    var dest = Path.Combine(systemDataDir, Path.GetFileName(dir));
                    Directory.Move(dir, dest);
                }
                LoggingService.Information("[Database] 旧数据已迁移 | From={From} | To={To}", legacyDataDir, systemDataDir);
            }
            catch (Exception ex)
            {
                LoggingService.Warning("[Database] 旧数据迁移失败，使用系统目录 | Error={Error}", ex.Message);
            }
        }

        if (!Directory.Exists(systemDataDir))
        {
            Directory.CreateDirectory(systemDataDir);
        }

        LoggingService.Information("[Database] 数据目录 | Path={Path}", systemDataDir);
        return systemDataDir;
    }

    /// <summary>
    /// Database connection string
    /// 数据库连接字符串
    /// </summary>
    public readonly static string Connection = $"datasource={DataDirectoryPath}/shmtu.terminal.{DbExtension}";

    public static string Password = "";

    public string DatabaseFileBaseName = "shmtu.terminal";

    /// <summary>
    /// Get a new SqlSugarClient instance with specific configurations
    /// 获取具有特定配置的新 SqlSugarClient 实例
    /// </summary>
    /// <returns>SqlSugarClient instance</returns>
    public static SqlSugarClient GetNewDb(string connectionString = "")
    {
        if (connectionString == "")
        {
            connectionString = Connection;
        }

        if (Password != "")
        {
            connectionString = new SqliteConnectionStringBuilder(connectionString)
            {
                Mode = SqliteOpenMode.ReadWrite,
                Password = Password
            }.ToString();
            LoggingService.Debug("[Database] 使用加密数据库连接");
        }

        var db = new SqlSugarClient(new ConnectionConfig()
            {
                IsAutoCloseConnection = true,
                DbType = DbType.Sqlite,
                ConnectionString = connectionString,
                LanguageType = LanguageType.Default
            },
            it =>
            {
                // Logging SQL statements and parameters before execution
                it.Aop.OnLogExecuting =
                    (sql, para)
                        =>
                    {
                        // Debug 级别日志：详细的 SQL 执行信息
                        LoggingService.Verbose("[SQL] {Sql} | Params={Params}",
                            UtilMethods.GetNativeSql(sql, para),
                            para != null ? string.Join(", ", para.Select(p => p?.ToString() ?? "null")) : "无");
                    };

                // SQL 执行完成后记录（用于调试慢查询）
                it.Aop.OnLogExecuted =
                    (sql, para) =>
                    {
                        // 记录执行时间超过 1 秒的查询
                        LoggingService.Debug("[SQL] 执行完成 | Sql={Sql}", UtilMethods.GetNativeSql(sql, para));
                    };

                // 错误回调
                it.Aop.OnError =
                    (exp) =>
                    {
                        LoggingService.Error(exp.Sql, "[SQL] SQL 执行错误 | Error={Error}", exp.Message);
                    };
            });

        LoggingService.Debug("[Database] 创建新的数据库连接 | Connection={Connection}",
            connectionString.Contains("Password") ? "[加密连接]" : connectionString);

        return db;
    }

    public static string GetAbsolutePath()
    {
        var connectionCfgList = Connection.Split(";");
        var path = "";

        foreach (var connectionCfg in connectionCfgList)
        {
            if (connectionCfg.Contains("datasource="))
            {
                path = connectionCfg.Replace("datasource=", "");
            }
        }

        // DataDirectoryPath 已经是绝对路径，无需再拼接
        LoggingService.Verbose("[Database] 数据库绝对路径 | Path={Path}", path);
        return path;
    }

    public SqlSugarClient GetNewDbObj()
    {
        var newConnection = $"datasource={DataDirectoryPath}/{DatabaseFileBaseName}.{DbExtension}";
        LoggingService.Debug("[Database] 创建数据库实例 | Connection={Connection}", newConnection);
        return GetNewDb(newConnection);
    }
}
