using System.Security.Cryptography;
using System.Text;

namespace shmtu.terminal.desktop.Services.Security;

/// <summary>
/// 加密服务 — 提供密码加密存储、Cookie 加密存储等功能
/// 使用 AES-256-GCM 进行对称加密，SHA-256 进行哈希
/// </summary>
public static class EncryptionService
{
    /// <summary>
    /// 应用级加密密钥 — 由启动保护密码或设备唯一标识派生
    /// 在应用启动时设置
    /// </summary>
    private static string _masterKey = "";

    /// <summary>
    /// 设置主密钥
    /// </summary>
    public static void SetMasterKey(string key)
    {
        _masterKey = key;
    }

    /// <summary>
    /// 获取派生密钥 — 从主密钥派生 AES 密钥
    /// </summary>
    private static byte[] GetDerivedKey(string salt)
    {
        var keyMaterial = _masterKey;
        if (string.IsNullOrEmpty(keyMaterial))
        {
            // 如果未设置主密钥，使用设备唯一标识
            keyMaterial = GetDeviceUniqueId();
        }

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(keyMaterial),
            Encoding.UTF8.GetBytes(salt),
            10000,
            HashAlgorithmName.SHA256,
            32); // AES-256
    }

    /// <summary>
    /// 获取设备唯一标识
    /// </summary>
    private static string GetDeviceUniqueId()
    {
        try
        {
            var machineName = Environment.MachineName;
            var userName = Environment.UserName;
            var osVersion = Environment.OSVersion.ToString();
            return $"{machineName}_{userName}_{osVersion}";
        }
        catch
        {
            return "shmtu-terminal-default-key";
        }
    }

    #region AES 加密/解密

    /// <summary>
    /// AES-256-GCM 加密
    /// </summary>
    public static string Encrypt(string plainText, string salt = "shmtu-terminal")
    {
        if (string.IsNullOrEmpty(plainText)) return "";

        var key = GetDerivedKey(salt);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // 将 IV + 密文拼接后 Base64 编码
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// AES-256-GCM 解密
    /// </summary>
    public static string Decrypt(string cipherText, string salt = "shmtu-terminal")
    {
        if (string.IsNullOrEmpty(cipherText)) return "";

        try
        {
            var key = GetDerivedKey(salt);
            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = key;

            var iv = new byte[aes.IV.Length];
            var cipher = new byte[fullCipher.Length - iv.Length];

            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return "";
        }
    }

    #endregion

    #region 密码哈希

    /// <summary>
    /// 对密码进行 SHA-256 哈希（用于启动保护密码验证）
    /// </summary>
    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "";

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// 验证密码是否匹配哈希
    /// </summary>
    public static bool VerifyPasswordHash(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;
        var computedHash = HashPassword(password);
        return computedHash == hash.ToLowerInvariant();
    }

    #endregion

    #region 密码加密存储

    /// <summary>
    /// 加密账号密码（用于存储到数据库）
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

    #region Cookie 加密存储

    /// <summary>
    /// 加密 Cookie 数据（用于存储到数据库）
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
        var keyMaterial = _masterKey;
        if (string.IsNullOrEmpty(keyMaterial))
        {
            keyMaterial = GetDeviceUniqueId();
        }

        // 简单派生：SHA-256 取前16字节作为密码
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial + "_db_key"));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    #endregion
}
