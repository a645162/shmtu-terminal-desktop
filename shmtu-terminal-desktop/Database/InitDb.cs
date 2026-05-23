using shmtu.terminal.desktop.Services;
using System.IO;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Database.Manage.Session;

namespace shmtu.terminal.desktop.Database;

/// <summary>
/// 数据库初始化 — 创建所有必要的目录和表
/// </summary>
public static class InitDb
{
    public static void Init()
    {
        LoggingService.Information("[InitDb] 开始数据库初始化");

        // 创建目录结构
        CreateDirectories();
        LoggingService.Debug("[InitDb] 数据目录创建完成");

        // 初始化主数据库表
        LoggingService.Debug("[InitDb] 初始化身份表...");
        IdentityDb.InitTable();
        LoggingService.Debug("[InitDb] 身份表初始化完成");

        LoggingService.Debug("[InitDb] 初始化账号表...");
        AccountDb.InitTable();
        LoggingService.Debug("[InitDb] 账号表初始化完成");

        // 初始化会话数据库表
        LoggingService.Debug("[InitDb] 初始化会话表...");
        SessionInfoDb.InitTable();
        LoggingService.Debug("[InitDb] 会话表初始化完成");

        // 注意：BillOriginal 和 BillMerged 的表在首次访问时按需创建
        // 因为每个账号/身份有独立的数据库文件
        LoggingService.Information("[InitDb] 数据库初始化完成");
    }

    /// <summary>
    /// 初始化指定账号的原始账单数据库
    /// </summary>
    public static void InitAccountDb(string accountId)
    {
        LoggingService.Debug("[InitDb] 初始化账号账单库 | AccountId={AccountId}", accountId);
        BillOriginalDb.InitTable(accountId);
        LoggingService.Debug("[InitDb] 账号账单库初始化完成 | AccountId={AccountId}", accountId);
    }

    /// <summary>
    /// 初始化指定身份的合并账单数据库
    /// </summary>
    public static void InitIdentityDb(int identityId)
    {
        LoggingService.Debug("[InitDb] 初始化身份合并账单库 | IdentityId={IdentityId}", identityId);
        BillMergedDb.InitTable(identityId);
        LoggingService.Debug("[InitDb] 身份合并账单库初始化完成 | IdentityId={IdentityId}", identityId);
    }

    /// <summary>
    /// 创建所有必要的数据目录
    /// </summary>
    private static void CreateDirectories()
    {
        var dataDir = Common.BaseDbSource.DataDirectoryPath;

        var directories = new[]
        {
            dataDir,
            Path.Combine(dataDir, "identity"),
            Path.Combine(dataDir, "account"),
            Path.Combine(dataDir, "snapshot"),
            Path.Combine(dataDir, "models"),
            Path.Combine(dataDir, "export"),
            Path.Combine(dataDir, "logs"),
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                LoggingService.Debug("[InitDb] 创建目录 | Path={Path}", dir);
                Directory.CreateDirectory(dir);
            }
        }

        LoggingService.Information("[InitDb] 数据目录结构检查完成 | DataDir={DataDir}", dataDir);
    }
}
