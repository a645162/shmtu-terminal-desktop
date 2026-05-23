using shmtu.terminal.desktop.Services;
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
        LoggingService.Debug("[SessionInfoDb] 初始化会话表");
        EnsureDirectoryExists();
        var db = GetDb();
        if (!db.DbMaintenance.IsAnyTable(typeof(SessionInfo).FullName))
        {
            LoggingService.Information("[SessionInfoDb] 创建会话表");
            db.CodeFirst.InitTables(typeof(SessionInfo));
        }
        else
        {
            LoggingService.Debug("[SessionInfoDb] 会话表已存在，跳过创建");
        }
    }

    private static void EnsureDirectoryExists()
    {
        var dataDir = shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath;
        if (!System.IO.Directory.Exists(dataDir))
        {
            LoggingService.Debug("[SessionInfoDb] 创建数据目录 | Path={Path}", dataDir);
            System.IO.Directory.CreateDirectory(dataDir);
        }
    }

    /// <summary>
    /// 根据学号获取会话信息
    /// </summary>
    public static SessionInfo? GetByAccountId(string accountId)
    {
        LoggingService.Debug("[SessionInfoDb] 根据学号获取会话 | AccountId={AccountId}", accountId);
        var db = GetDb();
        var result = db.Queryable<SessionInfo>()
            .Where(s => s.AccountId == accountId)
            .First();
        LoggingService.Debug("[SessionInfoDb] 会话查询完成 | Found={Found}", result != null);
        return result;
    }

    /// <summary>
    /// 保存会话（加密存储 cookies）
    /// </summary>
    public static void Save(string accountId, string cookies, string? loginTime = null, string? expireTime = null)
    {
        LoggingService.Debug("[SessionInfoDb] 保存会话 | AccountId={AccountId} | HasExpire={HasExpire}",
            accountId, expireTime != null);
        var db = GetDb();
        var encryptedCookies = EncryptionService.EncryptCookie(cookies);
        var now = DateTime.Now.ToString("o");

        var existing = GetByAccountId(accountId);
        if (existing != null)
        {
            LoggingService.Debug("[SessionInfoDb] 更新已有会话");
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
            LoggingService.Debug("[SessionInfoDb] 创建新会话");
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
        LoggingService.Information("[SessionInfoDb] 会话保存完成 | AccountId={AccountId}", accountId);
    }

    /// <summary>
    /// 获取解密后的 cookies
    /// </summary>
    public static string? GetDecryptedCookies(string accountId)
    {
        LoggingService.Debug("[SessionInfoDb] 获取解密后的 cookies | AccountId={AccountId}", accountId);
        var session = GetByAccountId(accountId);
        if (session == null)
        {
            LoggingService.Debug("[SessionInfoDb] 会话不存在");
            return null;
        }
        var result = EncryptionService.DecryptCookie(session.Cookies);
        LoggingService.Debug("[SessionInfoDb] Cookies 解密完成 | HasValue={HasValue}", !string.IsNullOrEmpty(result));
        return result;
    }

    /// <summary>
    /// 标记会话为无效
    /// </summary>
    public static void Invalidate(string accountId)
    {
        LoggingService.Debug("[SessionInfoDb] 标记会话无效 | AccountId={AccountId}", accountId);
        var db = GetDb();
        db.Updateable<SessionInfo>()
            .SetColumns(s => s.IsValid == false)
            .Where(s => s.AccountId == accountId)
            .ExecuteCommand();
        LoggingService.Information("[SessionInfoDb] 会话已标记为无效 | AccountId={AccountId}", accountId);
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    public static void Delete(string accountId)
    {
        LoggingService.Information("[SessionInfoDb] 删除会话 | AccountId={AccountId}", accountId);
        var db = GetDb();
        db.Deleteable<SessionInfo>()
            .Where(s => s.AccountId == accountId)
            .ExecuteCommand();
        LoggingService.Information("[SessionInfoDb] 会话删除完成");
    }

    /// <summary>
    /// 清除所有无效会话
    /// </summary>
    public static void ClearInvalid()
    {
        LoggingService.Debug("[SessionInfoDb] 清除所有无效会话");
        var db = GetDb();
        db.Deleteable<SessionInfo>()
            .Where(s => !s.IsValid)
            .ExecuteCommand();
        LoggingService.Information("[SessionInfoDb] 无效会话清除完成");
    }

    /// <summary>
    /// 获取所有有效会话
    /// </summary>
    public static List<SessionInfo> GetAllValid()
    {
        LoggingService.Debug("[SessionInfoDb] 获取所有有效会话");
        var db = GetDb();
        var result = db.Queryable<SessionInfo>()
            .Where(s => s.IsValid)
            .ToList();
        LoggingService.Debug("[SessionInfoDb] 有效会话查询完成 | Count={Count}", result.Count);
        return result;
    }
}
