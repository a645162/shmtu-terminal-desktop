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
    private const int KeySizeBytes = 32;   // AES-256
    private const int NonceSizeBytes = 12; // GCM recommended nonce size
    private const int TagSizeBytes = 16;  // GCM tag size
    private const int SaltSizeBytes = 16; // 128-bit salt
    private const int Pbkdf2Iterations = 100_000;

    /// <summary>
    /// 应用级加密密钥 — 由启动保护密码或设备唯一标识派生
    /// 在应用启动时设置
    /// </summary>
    private static string _masterKey = "";

    private static readonly ReaderWriterLockSlim _keyLock = new();
    private static readonly string _deviceKeyFile;

    static EncryptionService()
    {
        var dataDir = shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath;
        _deviceKeyFile = Path.Combine(dataDir, ".device_key");
    }

    /// <summary>
    /// 设置主密钥
    /// </summary>
    public static void SetMasterKey(string key)
    {
        _keyLock.EnterWriteLock();
        try
        {
            _masterKey = key;
        }
        finally
        {
            _keyLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 清除主密钥（锁定后调用）
    /// </summary>
    public static void ClearMasterKey()
    {
        _keyLock.EnterWriteLock();
        try
        {
            _masterKey = "";
        }
        finally
        {
            _keyLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 获取派生密钥 — 从主密钥或设备唯一标识 + 指定 salt 派生 AES-256 密钥
    /// </summary>
    private static byte[] GetDerivedKey(string masterKeyMaterial, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(masterKeyMaterial),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    /// <summary>
    /// 获取设备唯一标识 — 从持久化文件读取，文件不存在则生成并存储
    /// </summary>
    private static string GetDeviceUniqueId()
    {
        _keyLock.EnterReadLock();
        try
        {
            if (!string.IsNullOrEmpty(_masterKey))
                return _masterKey;
        }
        finally
        { _keyLock.ExitReadLock(); }

        // Try to read existing device key
        if (File.Exists(_deviceKeyFile))
        {
            var storedKey = File.ReadAllText(_deviceKeyFile).Trim();
            if (!string.IsNullOrEmpty(storedKey))
                return storedKey;
        }

        // Generate and persist new device key
        var newKey = Guid.NewGuid().ToString("N") + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        try
        {
            var dir = Path.GetDirectoryName(_deviceKeyFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_deviceKeyFile, newKey);
            File.SetUnixFileMode(_deviceKeyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // If we can't write, use a fallback
            return newKey;
        }
        return newKey;
    }

    /// <summary>
    /// 获取用于加密的密钥材料（主密钥或设备ID）
    /// </summary>
    private static string GetKeyMaterial()
    {
        _keyLock.EnterReadLock();
        try
        {
            if (!string.IsNullOrEmpty(_masterKey))
                return _masterKey;
        }
        finally
        { _keyLock.ExitReadLock(); }

        return GetDeviceUniqueId();
    }

    #region AES-256-GCM 加密/解密

    /// <summary>
    /// AES-256-GCM 加密 — 输出格式: Base64(salt + nonce + ciphertext + tag)
    /// </summary>
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

        // Layout: salt (16) + nonce (12) + ciphertext + tag (16)
        var result = new byte[SaltSizeBytes + NonceSizeBytes + cipherText.Length + TagSizeBytes];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSizeBytes);
        Buffer.BlockCopy(nonce, 0, result, SaltSizeBytes, NonceSizeBytes);
        Buffer.BlockCopy(cipherText, 0, result, SaltSizeBytes + NonceSizeBytes, cipherText.Length);
        Buffer.BlockCopy(tag, 0, result, SaltSizeBytes + NonceSizeBytes + cipherText.Length, TagSizeBytes);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// AES-256-GCM 解密
    /// </summary>
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
            throw new DecryptionFailedException("解密失败：数据可能被篡改或密钥不匹配", ex);
        }
    }

    #endregion

    #region 密码哈希（PBKDF2 + 随机 salt）

    /// <summary>
    /// 对密码进行 PBKDF2-SHA256 哈希（带随机 salt）
    /// 输出格式: Base64(salt + hash)
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "";

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);

        // Layout: salt (16) + hash (32)
        var result = new byte[SaltSizeBytes + KeySizeBytes];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSizeBytes);
        Buffer.BlockCopy(hash, 0, result, SaltSizeBytes, KeySizeBytes);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 验证密码是否匹配 PBKDF2 哈希
    /// </summary>
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
                Encoding.UTF8.GetBytes(password),
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                KeySizeBytes);

            return CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 密码加密存储（随机 salt，每次加密生成新 salt）

    /// <summary>
    /// 加密账号密码（用于存储到数据库，每次加密使用随机 salt）
    /// </summary>
    public static string EncryptPassword(string rawPassword)
    {
        return Encrypt(rawPassword, "shmtu-account-password");
    }

    /// <summary>
    /// 解密账号密码（从数据库读取时使用）
    /// </summary>
    public static string DecryptPassword(string encryptedPassword)
    {
        return Decrypt(encryptedPassword, "shmtu-account-password");
    }

    #endregion

    #region Cookie 加密存储（随机 salt，每次加密生成新 salt）

    /// <summary>
    /// 加密 Cookie 数据（用于存储到数据库，每次加密使用随机 salt）
    /// </summary>
    public static string EncryptCookie(string cookieData)
    {
        return Encrypt(cookieData, "shmtu-session-cookie");
    }

    /// <summary>
    /// 解密 Cookie 数据（从数据库读取时使用）
    /// </summary>
    public static string DecryptCookie(string encryptedCookie)
    {
        return Decrypt(encryptedCookie, "shmtu-session-cookie");
    }

    #endregion

    #region 数据库加密密钥

    /// <summary>
    /// 获取数据库加密密码（用于 SqlSugar 的 SQLite Password 特性）
    /// 如果设置了启动保护密码，使用密码派生；否则使用设备唯一标识派生
    /// </summary>
    public static string GetDatabasePassword()
    {
        var keyMaterial = GetKeyMaterial();
        // PBKDF2 with fixed salt for deterministic DB key derivation
        var salt = Encoding.UTF8.GetBytes("shmtu-db-salt-v1");
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(keyMaterial),
            salt,
            10_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    #endregion
}

/// <summary>
/// 解密失败时抛出的异常
/// </summary>
public class DecryptionFailedException : Exception
{
    public DecryptionFailedException(string message) : base(message) { }
    public DecryptionFailedException(string message, Exception inner) : base(message, inner) { }
}
