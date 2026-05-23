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
        var db = GetDb();
        if (!db.DbMaintenance.IsAnyTable(typeof(AccountInfo).FullName))
        {
            db.CodeFirst.InitTables(typeof(AccountInfo));
        }
    }

    /// <summary>
    /// 根据身份ID获取所有账号
    /// </summary>
    public static List<AccountInfo> GetByIdentityId(int identityId)
    {
        var db = GetDb();
        return db.Queryable<AccountInfo>()
            .Where(a => a.IdentityId == identityId)
            .OrderBy(a => a.Id)
            .ToList();
    }

    /// <summary>
    /// 根据身份ID获取启用的账号
    /// </summary>
    public static List<AccountInfo> GetEnabledByIdentityId(int identityId)
    {
        var db = GetDb();
        return db.Queryable<AccountInfo>()
            .Where(a => a.IdentityId == identityId && a.Enable)
            .OrderBy(a => a.Id)
            .ToList();
    }

    /// <summary>
    /// 根据ID获取账号
    /// </summary>
    public static AccountInfo? GetById(int id)
    {
        var db = GetDb();
        return db.Queryable<AccountInfo>()
            .Where(a => a.Id == id)
            .First();
    }

    /// <summary>
    /// 根据学号获取账号
    /// </summary>
    public static AccountInfo? GetByAccountId(string accountId)
    {
        var db = GetDb();
        return db.Queryable<AccountInfo>()
            .Where(a => a.AccountId == accountId)
            .First();
    }

    /// <summary>
    /// 添加账号（密码自动加密存储）
    /// </summary>
    public static int Add(AccountInfo account, string rawPassword)
    {
        var db = GetDb();
        account.Password = EncryptionService.EncryptPassword(rawPassword);
        account.CreatedAt = DateTime.Now.ToString("o");
        account.UpdatedAt = DateTime.Now.ToString("o");
        return db.Insertable(account).ExecuteReturnIdentity();
    }

    /// <summary>
    /// 更新账号信息
    /// </summary>
    public static bool Update(AccountInfo account)
    {
        var db = GetDb();
        account.UpdatedAt = DateTime.Now.ToString("o");
        return db.Updateable(account)
            .IgnoreColumns(a => a.Password) // 不更新密码字段
            .ExecuteCommand() > 0;
    }

    /// <summary>
    /// 更新账号密码（加密存储）
    /// </summary>
    public static bool UpdatePassword(int id, string rawPassword)
    {
        var db = GetDb();
        var encryptedPassword = EncryptionService.EncryptPassword(rawPassword);
        return db.Updateable<AccountInfo>()
            .SetColumns(a => a.Password == encryptedPassword)
            .SetColumns(a => a.UpdatedAt == DateTime.Now.ToString("o"))
            .Where(a => a.Id == id)
            .ExecuteCommand() > 0;
    }

    /// <summary>
    /// 获取解密后的密码
    /// </summary>
    public static string GetDecryptedPassword(int id)
    {
        var account = GetById(id);
        if (account == null) return "";
        return EncryptionService.DecryptPassword(account.Password);
    }

    /// <summary>
    /// 删除账号（同时删除对应的账单数据库文件）
    /// </summary>
    public static bool Delete(int id)
    {
        var db = GetDb();
        var account = GetById(id);
        if (account != null)
        {
            // 删除账号账单数据库文件
            var accountDbPath = System.IO.Path.Combine(
                BaseDbSource.DataDirectoryPath, "account", $"{account.AccountId}.sqlite");
            if (System.IO.File.Exists(accountDbPath))
            {
                System.IO.File.Delete(accountDbPath);
            }
        }

        return db.Deleteable<AccountInfo>()
            .Where(a => a.Id == id)
            .ExecuteCommand() > 0;
    }

    /// <summary>
    /// 更新最后同步时间
    /// </summary>
    public static void UpdateLastSyncTime(int id)
    {
        var db = GetDb();
        db.Updateable<AccountInfo>()
            .SetColumns(a => a.LastUpdateTime == DateTime.Now.ToString("o"))
            .SetColumns(a => a.UpdatedAt == DateTime.Now.ToString("o"))
            .Where(a => a.Id == id)
            .ExecuteCommand();
    }

    /// <summary>
    /// 获取所有账号（跨身份）
    /// </summary>
    public static List<AccountInfo> GetAll()
    {
        var db = GetDb();
        return db.Queryable<AccountInfo>()
            .OrderBy(a => a.Id)
            .ToList();
    }

    /// <summary>
    /// 验证学号格式（12位数字）
    /// </summary>
    public static bool ValidateAccountId(string accountId)
    {
        if (string.IsNullOrEmpty(accountId) || accountId.Length != 12)
            return false;
        return accountId.All(char.IsDigit);
    }
}
