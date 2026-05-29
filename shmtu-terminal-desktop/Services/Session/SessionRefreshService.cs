using System.Net;
using shmtu.cas.auth;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Database.Manage.Session;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Security;

namespace shmtu.terminal.desktop.Services.Session;

/// <summary>
/// Session 过期检查服务 — 后台定期检查 session 是否过期
/// 一旦检测到 session 过期，立即标记为无效，不再继续检查
/// </summary>
public class SessionExpirationService : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _runningTask;
    private readonly object _lock = new();

    private static SessionExpirationService? _instance;
    public static SessionExpirationService? Instance => _instance;

    public event Action<SessionExpirationEventArgs>? ExpirationCheck;

    /// <summary>
    /// 启动 session 过期检查服务
    /// </summary>
    public static void Start()
    {
        lock (typeof(SessionExpirationService))
        {
            if (_instance != null) return;

            var config = TomlConfigService.LoadAppConfig();
            if (!config.Session.AutoRefresh)
            {
                LoggingService.Debug("[SessionExpiration] 自动检查已禁用，跳过启动");
                return;
            }

            _instance = new SessionExpirationService();
            _instance.StartInternal();
            LoggingService.Information("[SessionExpiration] 启动成功 | Interval={Interval}分钟",
                config.Session.RefreshIntervalMinutes);
        }
    }

    /// <summary>
    /// 停止 session 过期检查服务
    /// </summary>
    public static void Stop()
    {
        lock (typeof(SessionExpirationService))
        {
            _instance?.Dispose();
            _instance = null;
            LoggingService.Information("[SessionExpiration] 已停止");
        }
    }

    /// <summary>
    /// 重启服务（配置变更后调用）
    /// </summary>
    public static void Restart()
    {
        Stop();
        Start();
    }

    private void StartInternal()
    {
        _cts = new CancellationTokenSource();
        _runningTask = Task.Run(async () =>
        {
            var random = new Random();
            var baseInterval = GetBaseIntervalMinutes();

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // 计算带浮动的间隔：N ± 1 分钟
                    var jitter = random.Next(-1, 2); // -1, 0, 或 1
                    var intervalMinutes = baseInterval + jitter;
                    var delay = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));

                    LoggingService.Verbose("[SessionExpiration] 下次检查 | Interval={Interval}分钟", intervalMinutes);

                    await Task.Delay(delay, _cts.Token);

                    if (_cts.Token.IsCancellationRequested) break;

                    // 执行 session 过期检查
                    await PerformCheckAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggingService.Error(ex, "[SessionExpiration] 检查异常");
                    // 出错后等待 1 分钟再重试
                    try { await Task.Delay(TimeSpan.FromMinutes(1), _cts.Token); } catch { }
                }
            }
        }, _cts.Token);
    }

    private static int GetBaseIntervalMinutes()
    {
        try
        {
            var config = TomlConfigService.LoadAppConfig();
            return Math.Max(1, config.Session.RefreshIntervalMinutes);
        }
        catch
        {
            return 10; // 默认值
        }
    }

    private async Task PerformCheckAsync()
    {
        LoggingService.Debug("[SessionExpiration] 开始检查 session 状态");

        var config = TomlConfigService.LoadAppConfig();
        var identityId = config.Identity.DefaultIdentityId;

        if (identityId == 0)
        {
            LoggingService.Verbose("[SessionExpiration] 无默认身份，跳过检查");
            return;
        }

        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        if (accounts.Count == 0)
        {
            LoggingService.Verbose("[SessionExpiration] 无启用的账号，跳过检查");
            return;
        }

        var checkResults = new List<AccountExpirationResult>();

        foreach (var account in accounts)
        {
            var result = await CheckAndInvalidateExpiredSessionAsync(account);
            checkResults.Add(result);

            // 如果 session 已过期并被标记为无效，停止对该账号的后续检查
            if (result.WasInvalidated)
            {
                LoggingService.Information("[SessionExpiration] 检测到过期 session 已标记为无效，停止后续检查 | AccountId={AccountId}",
                    account.AccountId);
            }
        }

        var validCount = checkResults.Count(r => r.IsValid);
        var expiredCount = checkResults.Count - validCount;

        LoggingService.Information("[SessionExpiration] 检查完成 | Total={Total} | Valid={Valid} | Expired={Expired}",
            checkResults.Count, validCount, expiredCount);

        ExpirationCheck?.Invoke(new SessionExpirationEventArgs
        {
            TotalAccounts = checkResults.Count,
            ValidCount = validCount,
            ExpiredCount = expiredCount,
            Results = checkResults,
        });
    }

    private async Task<AccountExpirationResult> CheckAndInvalidateExpiredSessionAsync(AccountInfo account)
    {
        var result = new AccountExpirationResult { AccountId = account.AccountId };

        try
        {
            // 1. 获取解密后的 session
            var cookies = SessionInfoDb.GetDecryptedCookies(account.AccountId);
            if (string.IsNullOrEmpty(cookies))
            {
                result.IsValid = false;
                result.Status = "no_session";
                LoggingService.Debug("[SessionExpiration] 无保存的 session | AccountId={AccountId}", account.AccountId);
                return result;
            }

            // 3. 创建 CAS 客户端并恢复 session
            using var casClient = new CasHttpClient();
            RestoreCookiesToContainer(casClient.CookieContainer, cookies);

            using var epayAuth = new EpayAuth(
                new ManualCaptchaResolver((imageData, ct) => Task.FromException<CaptchaAnswer>(
                    new InvalidOperationException("不需要验证码"))),
                casClient);

            // 4. 探测登录状态
            var probe = await epayAuth.ProbeLoginAsync();
            if (probe is LoginProbe.AlreadyLoggedIn)
            {
                result.IsValid = true;
                result.Status = "valid";
                LoggingService.Debug("[SessionExpiration] Session 有效 | AccountId={AccountId}", account.AccountId);
            }
            else
            {
                // Session 已过期，标记为无效
                LoggingService.Information("[SessionExpiration] 检测到过期 session，正在标记为无效 | AccountId={AccountId}", account.AccountId);
                SessionInfoDb.Invalidate(account.AccountId);
                result.IsValid = false;
                result.Status = "expired";
                result.WasInvalidated = true;
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Status = "error";
            result.ErrorMessage = ex.Message;
            LoggingService.Error(ex, "[SessionExpiration] 检查失败 | AccountId={AccountId}", account.AccountId);
        }

        return result;
    }

    private static void RestoreCookiesToContainer(CookieContainer container, string cookiesStr)
    {
        if (string.IsNullOrWhiteSpace(cookiesStr)) return;

        var cookieParts = cookiesStr.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var domain = ".shmtu.edu.cn";

        foreach (var cookiePair in cookieParts)
        {
            var trimmed = cookiePair.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0) continue;

            var name = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            try
            {
                container.Add(new Cookie(name, value) { Domain = domain });
            }
            catch
            {
                try { container.Add(new Cookie(name, value)); } catch { }
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _runningTask?.Wait(TimeSpan.FromSeconds(5));
        _runningTask?.Dispose();
        _cts = null;
        _runningTask = null;
    }
}

/// <summary>
/// Session 过期检查事件参数
/// </summary>
public class SessionExpirationEventArgs : EventArgs
{
    public int TotalAccounts { get; set; }
    public int ValidCount { get; set; }
    public int ExpiredCount { get; set; }
    public List<AccountExpirationResult> Results { get; set; } = [];
}

/// <summary>
/// 单个账号的过期检查结果
/// </summary>
public class AccountExpirationResult
{
    public string AccountId { get; set; } = "";
    public bool IsValid { get; set; }
    public string Status { get; set; } = "";
    /// <summary>
    /// 是否已被标记为失效
    /// </summary>
    public bool WasInvalidated { get; set; }
    public string? ErrorMessage { get; set; }
}
