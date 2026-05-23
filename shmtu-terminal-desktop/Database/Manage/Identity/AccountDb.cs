using shmtu.terminal.desktop.Services;
using shmtu.terminal.desktop.Database.Common;
using shmtu.terminal.desktop.Database.Source;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Services.Security;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Identity;

/// <summary>
/// 账号数据库 CRUD 操作
/// 账号信息存储在主数据库 shmtu.terminal.sqlite 中
/// 密码加密存储
/// </summary>
public static class AccountDb
{
    private static SqlSugarClient GetDb() => BaseDbSource.GetNewDb();

    /// <summary>
    /// 初始化账号表
    /// </summary>
    public static void InitTable()
    {
        LoggingService.Debug("[AccountDb] 初始化账号表");
        var db = GetDb();
        if (!db.DbMaintenance.IsAnyTable(typeof(AccountInfo).FullName))
        {
            LoggingService.Information("[AccountDb] 创建账号表");
            db.CodeFirst.InitTables(typeof(AccountInfo));
        }
        else
        {
            LoggingService.Debug("[AccountDb] 账号表已存在，跳过创建");
        }
    }

    /// <summary>
    /// 根据身份ID获取所有账号
    /// </summary>
    public static List<AccountInfo> GetByIdentityId(int identityId)
    {
        LoggingService.Verbose("[AccountDb] 根据身份ID获取账号 | IdentityId={IdentityId}", identityId);
        var db = GetDb();
        var result = db.Queryable<AccountInfo>()
            .Where(a => a.IdentityId == identityId)
            .OrderBy(a => a.Id)
            .ToList();
        LoggingService.Debug("[AccountDb] 获取账号完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 根据身份ID获取启用的账号
    /// </summary>
    public static List<AccountInfo> GetEnabledByIdentityId(int identityId)
    {
        LoggingService.Debug("[AccountDb] 根据身份ID获取启用的账号 | IdentityId={IdentityId}", identityId);
        var db = GetDb();
        var result = db.Queryable<AccountInfo>()
            .Where(a => a.IdentityId == identityId && a.Enable)
            .OrderBy(a => a.Id)
            .ToList();
        LoggingService.Debug("[AccountDb] 获取启用的账号完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 根据ID获取账号
    /// </summary>
    public static AccountInfo? GetById(int id)
    {
        LoggingService.Verbose("[AccountDb] 根据ID获取账号 | Id={Id}", id);
        var db = GetDb();
        var result = db.Queryable<AccountInfo>()
            .Where(a => a.Id == id)
            .First();
        LoggingService.Verbose("[AccountDb] 获取结果 | Found={Found} | AccountId={AccountId}",
            result != null, result?.AccountId);
        return result;
    }

    /// <summary>
    /// 根据学号获取账号
    /// </summary>
    public static AccountInfo? GetByAccountId(string accountId)
    {
        LoggingService.Debug("[AccountDb] 根据学号获取账号 | AccountId={AccountId}", accountId);
        var db = GetDb();
        var result = db.Queryable<AccountInfo>()
            .Where(a => a.AccountId == accountId)
            .First();
        LoggingService.Debug("[AccountDb] 获取结果 | Found={Found}", result != null);
        return result;
    }

    /// <summary>
    /// 添加账号（密码自动加密存储）
    /// </summary>
    public static int Add(AccountInfo account, string rawPassword)
    {
        LoggingService.Information("[AccountDb] 添加账号 | AccountId={AccountId} | IdentityId={IdentityId}",
            account.AccountId, account.IdentityId);
        var db = GetDb();
        account.Password = EncryptionService.EncryptPassword(rawPassword);
        account.CreatedAt = DateTime.Now.ToString("o");
        account.UpdatedAt = DateTime.Now.ToString("o");
        var result = db.Insertable(account).ExecuteReturnIdentity();
        LoggingService.Information("[AccountDb] 账号添加成功 | Id={Id}", result);
        return result;
    }

    /// <summary>
    /// 更新账号信息（不更新密码）
    /// </summary>
    public static bool Update(AccountInfo account)
    {
        LoggingService.Information("[AccountDb] 更新账号信息 | Id={Id} | AccountId={AccountId}",
            account.Id, account.AccountId);
        var db = GetDb();
        account.UpdatedAt = DateTime.Now.ToString("o");
        var result = db.Updateable(account)
            .IgnoreColumns(a => a.Password)
            .ExecuteCommand() > 0;
        LoggingService.Information("[AccountDb] 账号更新完成 | Success={Success}", result);
        return result;
    }

    /// <summary>
    /// 更新账号信息（可选更新密码）
    /// </summary>
    public static bool Update(AccountInfo account, string newPassword)
    {
        LoggingService.Information("[AccountDb] 更新账号信息（含密码） | Id={Id} | UpdatePassword={HasPassword}",
            account.Id, !string.IsNullOrWhiteSpace(newPassword));
        var db = GetDb();
        account.UpdatedAt = DateTime.Now.ToString("o");

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            LoggingService.Debug("[AccountDb] 不更新密码字段");
            var result = db.Updateable(account)
                .IgnoreColumns(a => a.Password)
                .ExecuteCommand() > 0;
            LoggingService.Information("[AccountDb] 账号更新完成 | Success={Success}", result);
            return result;
        }
        else
        {
            LoggingService.Debug("[AccountDb] 更新密码字段");
            account.Password = EncryptionService.EncryptPassword(newPassword);
            var result = db.Updateable(account)
                .ExecuteCommand() > 0;
            LoggingService.Information("[AccountDb] 账号更新完成 | Success={Success}", result);
            return result;
        }
    }

    /// <summary>
    /// 更新账号密码（加密存储）
    /// </summary>
    public static bool UpdatePassword(int id, string rawPassword)
    {
        LoggingService.Information("[AccountDb] 更新账号密码 | Id={Id}", id);
        var db = GetDb();
        var encryptedPassword = EncryptionService.EncryptPassword(rawPassword);
        var result = db.Updateable<AccountInfo>()
            .SetColumns(a => a.Password == encryptedPassword)
            .SetColumns(a => a.UpdatedAt == DateTime.Now.ToString("o"))
            .Where(a => a.Id == id)
            .ExecuteCommand() > 0;
        LoggingService.Information("[AccountDb] 密码更新完成 | Success={Success}", result);
        return result;
    }

    /// <summary>
    /// 获取解密后的密码
    /// </summary>
    public static string GetDecryptedPassword(int id)
    {
        LoggingService.Debug("[AccountDb] 获取解密后的密码 | Id={Id}", id);
        var account = GetById(id);
        if (account == null)
        {
            LoggingService.Warning("[AccountDb] 账号不存在 | Id={Id}", id);
            return "";
        }
        var result = EncryptionService.DecryptPassword(account.Password);
        LoggingService.Debug("[AccountDb] 密码解密完成");
        return result;
    }

    /// <summary>
    /// 删除账号（同时删除对应的账单数据库文件）
    /// </summary>
    public static bool Delete(int id)
    {
        LoggingService.Warning("[AccountDb] 删除账号 | Id={Id}", id);
        var db = GetDb();
        var account = GetById(id);
        if (account != null)
        {
            LoggingService.Debug("[AccountDb] 删除账号前先删除账单数据库文件 | AccountId={AccountId}", account.AccountId);
            // 删除账号账单数据库文件
            var accountDbPath = System.IO.Path.Combine(
                BaseDbSource.DataDirectoryPath, "account", $"{account.AccountId}.sqlite");
            if (System.IO.File.Exists(accountDbPath))
            {
                LoggingService.Debug("[AccountDb] 删除账单数据库文件 | Path={Path}", accountDbPath);
                System.IO.File.Delete(accountDbPath);
            }
        }
        else
        {
            LoggingService.Warning("[AccountDb] 未找到要删除的账号 | Id={Id}", id);
        }

        var result = db.Deleteable<AccountInfo>()
            .Where(a => a.Id == id)
            .ExecuteCommand() > 0;
        LoggingService.Information("[AccountDb] 账号删除完成 | Success={Success}", result);
        return result;
    }

    /// <summary>
    /// 更新最后同步时间
    /// </summary>
    public static void UpdateLastSyncTime(int id)
    {
        LoggingService.Debug("[AccountDb] 更新最后同步时间 | Id={Id}", id);
        var db = GetDb();
        db.Updateable<AccountInfo>()
            .SetColumns(a => a.LastUpdateTime == DateTime.Now.ToString("o"))
            .SetColumns(a => a.UpdatedAt == DateTime.Now.ToString("o"))
            .Where(a => a.Id == id)
            .ExecuteCommand();
        LoggingService.Debug("[AccountDb] 最后同步时间更新完成");
    }

    /// <summary>
    /// 获取所有账号（跨身份）
    /// </summary>
    public static List<AccountInfo> GetAll()
    {
        LoggingService.Debug("[AccountDb] 获取所有账号");
        var db = GetDb();
        var result = db.Queryable<AccountInfo>()
            .OrderBy(a => a.Id)
            .ToList();
        LoggingService.Information("[AccountDb] 获取所有账号完成 | Count={Count}", result.Count);
        return result;
    }

    /// <summary>
    /// 验证学号格式（12位数字）
    /// </summary>
    public static bool ValidateAccountId(string accountId)
    {
        LoggingService.Verbose("[AccountDb] 验证学号格式 | AccountId={AccountId}", accountId);
        if (string.IsNullOrEmpty(accountId) || accountId.Length != 12)
        {
            LoggingService.Debug("[AccountDb] 学号格式验证失败 | Reason=长度不正确 | Length={Length}",
                accountId?.Length ?? 0);
            return false;
        }
        var result = accountId.All(char.IsDigit);
        LoggingService.Debug("[AccountDb] 学号格式验证完成 | Valid={Valid}", result);
        return result;
    }
}
