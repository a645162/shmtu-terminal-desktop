using shmtu.terminal.desktop.Database.Source.Session;
using shmtu.terminal.desktop.Models.Session;
using shmtu.terminal.desktop.Services.Security;
using SqlSugar;

namespace shmtu.terminal.desktop.Database.Manage.Session;

/// <summary>
/// 会话信息数据库 CRUD 操作
/// 存储 cookies 等会话信息，加密存储
/// 数据库文件：Data/session.sqlite
/// </summary>
public static class SessionInfoDb
{
    private static SqlSugarClient GetDb()
    {
        return new SessionDbSource().GetNewDbObj();
    }

    /// <summary>
    /// 初始化会话表
    /// </summary>
    public static void InitTable()
    {
        EnsureDirectoryExists();
        var db = GetDb();
        if (!db.DbMaintenance.IsAnyTable(typeof(SessionInfo).FullName))
        {
            db.CodeFirst.InitTables(typeof(SessionInfo));
        }
    }

    private static void EnsureDirectoryExists()
    {
        if (!System.IO.Directory.Exists(
                shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath))
        {
            System.IO.Directory.CreateDirectory(
                shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath);
        }
    }

    /// <summary>
    /// 根据学号获取会话信息
    /// </summary>
    public static SessionInfo? GetByAccountId(string accountId)
    {
        var db = GetDb();
        return db.Queryable<SessionInfo>()
            .Where(s => s.AccountId == accountId)
            .First();
    }

    /// <summary>
    /// 保存会话（加密存储 cookies）
    /// </summary>
    public static void Save(string accountId, string cookies, string? loginTime = null, string? expireTime = null)
    {
        var db = GetDb();
        var encryptedCookies = EncryptionService.EncryptCookie(cookies);
        var now = DateTime.Now.ToString("o");

        var existing = GetByAccountId(accountId);
        if (existing != null)
        {
            db.Updateable<SessionInfo>()
                .SetColumns(s => s.Cookies == encryptedCookies)
                .SetColumns(s => s.LoginTime == (loginTime ?? now))
                .SetColumns(s => s.ExpireTime == (expireTime ?? ""))
                .SetColumns(s => s.IsValid == true)
                .Where(s => s.AccountId == accountId)
                .ExecuteCommand();
        }
        else
        {
            var session = new SessionInfo
            {
                AccountId = accountId,
                Cookies = encryptedCookies,
                LoginTime = loginTime ?? now,
                ExpireTime = expireTime ?? "",
                IsValid = true,
            };
            db.Insertable(session).ExecuteCommand();
        }
    }

    /// <summary>
    /// 获取解密后的 cookies
    /// </summary>
    public static string? GetDecryptedCookies(string accountId)
    {
        var session = GetByAccountId(accountId);
        if (session == null) return null;
        return EncryptionService.DecryptCookie(session.Cookies);
    }

    /// <summary>
    /// 标记会话为无效
    /// </summary>
    public static void Invalidate(string accountId)
    {
        var db = GetDb();
        db.Updateable<SessionInfo>()
            .SetColumns(s => s.IsValid == false)
            .Where(s => s.AccountId == accountId)
            .ExecuteCommand();
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    public static void Delete(string accountId)
    {
        var db = GetDb();
        db.Deleteable<SessionInfo>()
            .Where(s => s.AccountId == accountId)
            .ExecuteCommand();
    }

    /// <summary>
    /// 清除所有无效会话
    /// </summary>
    public static void ClearInvalid()
    {
        var db = GetDb();
        db.Deleteable<SessionInfo>()
            .Where(s => !s.IsValid)
            .ExecuteCommand();
    }

    /// <summary>
    /// 获取所有有效会话
    /// </summary>
    public static List<SessionInfo> GetAllValid()
    {
        var db = GetDb();
        return db.Queryable<SessionInfo>()
            .Where(s => s.IsValid)
            .ToList();
    }
}
