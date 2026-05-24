using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace shmtu.terminal.desktop.Services.Security;

/// <summary>
/// 加密服务 — 提供密码加密存储、Cookie 加密存储等功能
/// 使用 AES-256-GCM 进行对称加密（真正的认证加密）
/// SHA-256 PBKDF2 进行密码哈希（带随机 salt）
/// </summary>
public static class EncryptionService
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int SaltSizeBytes = 16;
    private const int Pbkdf2Iterations = 100_000;

    private static string _masterKey = "";
    private static readonly ReaderWriterLockSlim _keyLock = new();
    private static readonly string _deviceKeyFile;

    static EncryptionService()
    {
        var dataDir = shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath;
        _deviceKeyFile = Path.Combine(dataDir, ".device_key");
    }

    public static void SetMasterKey(string key)
    {
        _keyLock.EnterWriteLock();
        try { _masterKey = key; }
        finally { _keyLock.ExitWriteLock(); }
    }

    public static void ClearMasterKey()
    {
        _keyLock.EnterWriteLock();
        try { _masterKey = ""; }
        finally { _keyLock.ExitWriteLock(); }
    }

    private static byte[] GetDerivedKey(string masterKeyMaterial, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(masterKeyMaterial), salt,
            Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
    }

    private static string GetDeviceUniqueId()
    {
        _keyLock.EnterReadLock();
        try { if (!string.IsNullOrEmpty(_masterKey)) return _masterKey; }
        finally { _keyLock.ExitReadLock(); }

        if (File.Exists(_deviceKeyFile))
        {
            var storedKey = File.ReadAllText(_deviceKeyFile).Trim();
            if (!string.IsNullOrEmpty(storedKey)) return storedKey;
        }

        var newKey = Guid.NewGuid().ToString("N") + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        try
        {
            var dir = Path.GetDirectoryName(_deviceKeyFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_deviceKeyFile, newKey);
            File.SetUnixFileMode(_deviceKeyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { return newKey; }
        return newKey;
    }

    private static string GetKeyMaterial()
    {
        _keyLock.EnterReadLock();
        try { if (!string.IsNullOrEmpty(_masterKey)) return _masterKey; }
        finally { _keyLock.ExitReadLock(); }
        return GetDeviceUniqueId();
    }

    public static string Encrypt(string plainText, string? fixedSalt = null)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var salt = fixedSalt != null
            ? Encoding.UTF8.GetBytes(fixedSalt.PadRight(SaltSizeBytes, '\0'))[..SaltSizeBytes]
            : RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var keyMaterial = GetKeyMaterial();
        var key = GetDerivedKey(keyMaterial, salt);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherText = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];
        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherText, tag);
        var result = new byte[SaltSizeBytes + NonceSizeBytes + cipherText.Length + TagSizeBytes];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSizeBytes);
        Buffer.BlockCopy(nonce, 0, result, SaltSizeBytes, NonceSizeBytes);
        Buffer.BlockCopy(cipherText, 0, result, SaltSizeBytes + NonceSizeBytes, cipherText.Length);
        Buffer.BlockCopy(tag, 0, result, SaltSizeBytes + NonceSizeBytes + cipherText.Length, TagSizeBytes);
        return Convert.ToBase64String(result);
    }

    public static string Decrypt(string cipherText, string? fixedSalt = null)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            var salt = new byte[SaltSizeBytes];
            var nonce = new byte[NonceSizeBytes];
            var tag = new byte[TagSizeBytes];
            var cipherLen = fullCipher.Length - SaltSizeBytes - NonceSizeBytes - TagSizeBytes;
            var cipher = new byte[cipherLen];
            Buffer.BlockCopy(fullCipher, 0, salt, 0, SaltSizeBytes);
            Buffer.BlockCopy(fullCipher, SaltSizeBytes, nonce, 0, NonceSizeBytes);
            Buffer.BlockCopy(fullCipher, SaltSizeBytes + NonceSizeBytes, cipher, 0, cipherLen);
            Buffer.BlockCopy(fullCipher, fullCipher.Length - TagSizeBytes, tag, 0, TagSizeBytes);
            var keyMaterial = GetKeyMaterial();
            var key = GetDerivedKey(keyMaterial, salt);
            var plainBytes = new byte[cipherLen];
            using var aesGcm = new AesGcm(key, TagSizeBytes);
            aesGcm.Decrypt(nonce, cipher, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException ex)
        {
            LoggingService.Fatal(ex, "[Encryption] 致命错误：解密失败 | Salt={Salt}", fixedSalt ?? "dynamic");
            throw new DecryptionFailedException(
                "解密失败：数据可能被篡改或密钥不匹配。请使用 DataResetUtility 重置所有数据。", ex);
        }
    }

    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "";
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        var result = new byte[SaltSizeBytes + KeySizeBytes];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSizeBytes);
        Buffer.BlockCopy(hash, 0, result, SaltSizeBytes, KeySizeBytes);
        return Convert.ToBase64String(result);
    }

    public static bool VerifyPasswordHash(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) return false;
        try
        {
            var data = Convert.FromBase64String(storedHash);
            if (data.Length != SaltSizeBytes + KeySizeBytes) return false;
            var salt = new byte[SaltSizeBytes];
            Buffer.BlockCopy(data, 0, salt, 0, SaltSizeBytes);
            var storedHashBytes = new byte[KeySizeBytes];
            Buffer.BlockCopy(data, SaltSizeBytes, storedHashBytes, 0, KeySizeBytes);
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
        }
        catch { return false; }
    }

    public static string EncryptPassword(string rawPassword) => Encrypt(rawPassword, "shmtu-account-password");
    public static string DecryptPassword(string encryptedPassword) => Decrypt(encryptedPassword, "shmtu-account-password");
    public static string EncryptCookie(string cookieData) => Encrypt(cookieData, "shmtu-session-cookie");
    public static string DecryptCookie(string encryptedCookie) => Decrypt(encryptedCookie, "shmtu-session-cookie");

    public static string GetDatabasePassword()
    {
        var keyMaterial = GetKeyMaterial();
        var salt = Encoding.UTF8.GetBytes("shmtu-db-salt-v1");
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(keyMaterial), salt, 10_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    #region 数据清除（致命错误后的恢复）

    /// <summary>
    /// 从主数据库获取所有账号的学号
    /// </summary>
    private static List<string> GetAllAccountIds(string mainDbPath)
    {
        var accountIds = new List<string>();
        if (!File.Exists(mainDbPath)) return accountIds;
        try
        {
            using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={mainDbPath}");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT AccountId FROM account";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var accountId = reader.GetString(0);
                if (!string.IsNullOrEmpty(accountId)) accountIds.Add(accountId);
            }
        }
        catch { /* 忽略错误 */ }
        return accountIds;
    }

    /// <summary>
    /// 清除所有加密相关数据（用于解密失败后的恢复）
    /// 包括：设备密钥文件、会话数据库、所有账号密码数据
    /// </summary>
    public static bool ClearAllData()
    {
        LoggingService.Fatal("[Encryption] ===== 开始清除所有加密数据 =====");

        try
        {
            var dataDir = shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath;
            var errors = new List<string>();

            // 1. 删除设备密钥文件
            if (File.Exists(_deviceKeyFile))
            {
                try { File.Delete(_deviceKeyFile); LoggingService.Fatal("[Reset] 已删除设备密钥文件"); }
                catch (Exception ex) { errors.Add($"设备密钥: {ex.Message}"); }
            }

            // 2. 删除会话数据库
            var sessionDbPath = Path.Combine(dataDir, "session.sqlite");
            if (File.Exists(sessionDbPath))
            {
                try { File.Delete(sessionDbPath); LoggingService.Fatal("[Reset] 已删除会话数据库"); }
                catch (Exception ex) { errors.Add($"会话数据库: {ex.Message}"); }
            }

            // 3. 清除主数据库中的加密字段
            var mainDbPath = Path.Combine(dataDir, "shmtu.terminal.sqlite");
            if (File.Exists(mainDbPath))
            {
                try
                {
                    using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={mainDbPath}");
                    db.Open();
                    using var cmd1 = db.CreateCommand();
                    cmd1.CommandText = "UPDATE account SET Password = ''"; cmd1.ExecuteNonQuery();
                    LoggingService.Fatal("[Reset] 已清除账号密码字段");
                    using var cmd2 = db.CreateCommand();
                    cmd2.CommandText = "DELETE FROM session_info WHERE 1=1"; cmd2.ExecuteNonQuery();
                    LoggingService.Fatal("[Reset] 已清除会话信息（或表不存在）");
                }
                catch (Exception ex) { errors.Add($"主数据库: {ex.Message}"); }
            }

            // 4. 删除账号数据库文件（按学号命名：Data/account/<account_id>.sqlite）
            var accountDir = Path.Combine(dataDir, "account");
            if (Directory.Exists(accountDir))
            {
                var accountIds = GetAllAccountIds(mainDbPath);
                LoggingService.Fatal("[Reset] 发现 {Count} 个账号待删除", accountIds.Count);

                foreach (var accountId in accountIds)
                {
                    var accountDbPath = Path.Combine(accountDir, $"{accountId}.sqlite");
                    if (File.Exists(accountDbPath))
                    {
                        try { File.Delete(accountDbPath); LoggingService.Fatal("[Reset] 已删除账号数据库 | AccountId={AccountId}", accountId); }
                        catch (Exception ex) { errors.Add($"账号数据库 {accountId}: {ex.Message}"); }
                    }
                }
            }

            // 5. 删除身份数据库文件（按学号命名：Data/identity/<student_id>.sqlite）
            var identityDir = Path.Combine(dataDir, "identity");
            if (Directory.Exists(identityDir))
            {
                foreach (var file in Directory.GetFiles(identityDir, "*.sqlite"))
                {
                    try { File.Delete(file); LoggingService.Fatal("[Reset] 已删除身份数据库 | File={File}", Path.GetFileName(file)); }
                    catch (Exception ex) { errors.Add($"身份数据库: {ex.Message}"); }
                }
            }

            if (errors.Count > 0)
            {
                LoggingService.Fatal("[Reset] 数据清除完成，但有错误: {Errors}", string.Join("; ", errors));
                return false;
            }

            LoggingService.Fatal("[Reset] ===== 所有加密数据清除完成！请重启应用 =====");
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.Fatal(ex, "[Encryption] 数据清除过程中发生异常");
            return false;
        }
    }

    #endregion
}

public class DecryptionFailedException : Exception
{
    public DecryptionFailedException(string message) : base(message) { }
    public DecryptionFailedException(string message, Exception inner) : base(message, inner) { }
}
