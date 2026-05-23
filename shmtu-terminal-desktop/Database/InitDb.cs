using System.IO;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Database.Manage.Session;
using shmtu.terminal.desktop.Database.Manage.User;

namespace shmtu.terminal.desktop.Database;

/// <summary>
/// 数据库初始化 — 创建所有必要的目录和表
/// </summary>
public static class InitDb
{
    public static void Init()
    {
        // 创建目录结构
        CreateDirectories();

        // 初始化主数据库表
        UserConfigureDb.Init();
        IdentityDb.InitTable();
        AccountDb.InitTable();

        // 初始化会话数据库表
        SessionInfoDb.InitTable();

        // 注意：BillOriginal 和 BillMerged 的表在首次访问时按需创建
        // 因为每个账号/身份有独立的数据库文件
    }

    /// <summary>
    /// 初始化指定账号的原始账单数据库
    /// </summary>
    public static void InitAccountDb(string accountId)
    {
        BillOriginalDb.InitTable(accountId);
    }

    /// <summary>
    /// 初始化指定身份的合并账单数据库
    /// </summary>
    public static void InitIdentityDb(int identityId)
    {
        BillMergedDb.InitTable(identityId);
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
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
