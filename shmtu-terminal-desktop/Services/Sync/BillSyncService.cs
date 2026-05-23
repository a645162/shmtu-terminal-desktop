using shmtu.cas.auth;
using shmtu.cas.auth.common;
using shmtu.cas.captcha;
using shmtu.datatype.bill;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Database.Manage.Session;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Models.Config;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Services.Config;
using shmtu.parser.bill;
using shmtu.sync;
using System.Text.Json;

namespace shmtu.terminal.desktop.Services.Sync;

/// <summary>
/// 同步进度信息
/// </summary>
public class SyncProgress
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int NewCount { get; set; }
    public string Status { get; set; } = ""; // logging_in, syncing, completed, failed
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 同步结果汇总
/// </summary>
public class SyncSummary
{
    public int TotalAccounts { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalNewBills { get; set; }
    public List<AccountSyncResult> AccountResults { get; set; } = [];
}

/// <summary>
/// 单账号同步结果
/// </summary>
public class AccountSyncResult
{
    public string AccountId { get; set; } = "";
    public bool Success { get; set; }
    public int NewCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 账单同步服务 — 封装增量同步逻辑
/// 支持单账户同步、身份级别同步、全量更新
/// </summary>
public class BillSyncService
{
    private readonly AppConfig _appConfig;

    public BillSyncService()
    {
        _appConfig = TomlConfigService.LoadAppConfig();
    }

    /// <summary>
    /// 同步进度变化事件
    /// </summary>
    public event Action<SyncProgress>? ProgressChanged;

    /// <summary>
    /// 单账号增量同步
    /// </summary>
    public async Task<AccountSyncResult> SyncAccountAsync(
        AccountInfo account,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        if (!account.Enable || !account.EnableUpdate)
        {
            return new AccountSyncResult
            {
                AccountId = account.AccountId,
                Success = true,
                NewCount = 0,
            };
        }

        try
        {
            // 获取解密后的密码
            var password = AccountDb.GetDecryptedPassword(account.Id);

            // 确保账号数据库已初始化
            InitDb.InitAccountDb(account.AccountId);

            // 确保身份数据库已初始化
            InitDb.InitIdentityDb(account.IdentityId);

            // 创建 BillStore 实现
            var store = new DatabaseBillStore(account.AccountId, account.IdentityId);

            // 尝试恢复或创建 EpayAuth
            using var epayAuth = await CreateEpayAuthAsync(account.AccountId, password, captchaResolver, cancellationToken);

            // 创建同步选项
            var syncOptions = new SyncOptions
            {
                StartPage = 1,
                MaxPages = _appConfig.Sync.MaxPages,
                BillType = BillType.All,
                EarlyStopThreshold = _appConfig.Sync.EarlyStopThreshold,
            };

            // 执行增量同步
            var syncResult = await BillSync.IncrementalSyncAsync(
                epayAuth, store, syncOptions, cancellationToken);

            // 保存会话 cookies
            await SaveSessionAsync(epayAuth, account.AccountId);

            // 更新最后同步时间
            AccountDb.UpdateLastSyncTime(account.Id);

            return new AccountSyncResult
            {
                AccountId = account.AccountId,
                Success = true,
                NewCount = syncResult.NewCount,
            };
        }
        catch (Exception ex)
        {
            return new AccountSyncResult
            {
                AccountId = account.AccountId,
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <summary>
    /// 身份级别增量同步 — 遍历身份下所有启用账号
    /// </summary>
    public async Task<SyncSummary> SyncIdentityAsync(
        int identityId,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        return await SyncAccountsAsync(accounts, captchaResolver, cancellationToken);
    }

    /// <summary>
    /// 单账号全量更新
    /// </summary>
    public async Task<AccountSyncResult> FullUpdateAccountAsync(
        AccountInfo account,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        if (!account.Enable) return new AccountSyncResult { AccountId = account.AccountId };

        try
        {
            var password = AccountDb.GetDecryptedPassword(account.Id);
            InitDb.InitAccountDb(account.AccountId);
            InitDb.InitIdentityDb(account.IdentityId);

            var store = new DatabaseBillStore(account.AccountId, account.IdentityId);
            using var epayAuth = await CreateEpayAuthAsync(account.AccountId, password, captchaResolver, cancellationToken);

            // 全量同步 — 不提前停止
            var syncOptions = new SyncOptions
            {
                StartPage = 1,
                MaxPages = _appConfig.Sync.MaxPages,
                BillType = BillType.All,
                EarlyStopThreshold = int.MaxValue, // 不提前停止
            };

            var syncResult = await BillSync.IncrementalSyncAsync(
                epayAuth, store, syncOptions, cancellationToken);

            // 全量更新后清空该账号的操作日志
            OperationLogDb.ClearByAccountId(account.IdentityId, account.AccountId);

            await SaveSessionAsync(epayAuth, account.AccountId);
            AccountDb.UpdateLastSyncTime(account.Id);

            return new AccountSyncResult
            {
                AccountId = account.AccountId,
                Success = true,
                NewCount = syncResult.NewCount,
            };
        }
        catch (Exception ex)
        {
            return new AccountSyncResult
            {
                AccountId = account.AccountId,
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    /// <summary>
    /// 身份级别全量更新
    /// </summary>
    public async Task<SyncSummary> FullUpdateIdentityAsync(
        int identityId,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        var summary = await SyncAccountsAsync(accounts, captchaResolver, cancellationToken);

        // 全量更新后清空该身份所有操作日志
        OperationLogDb.ClearAll(identityId);

        return summary;
    }

    /// <summary>
    /// 批量同步多个账号
    /// </summary>
    private async Task<SyncSummary> SyncAccountsAsync(
        List<AccountInfo> accounts,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken)
    {
        var summary = new SyncSummary
        {
            TotalAccounts = accounts.Count,
        };

        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OnProgressChanged(new SyncProgress
            {
                AccountId = account.AccountId,
                AccountName = account.AccountName,
                Status = "syncing",
            });

            var result = await SyncAccountAsync(account, captchaResolver, cancellationToken);

            if (result.Success)
            {
                summary.SuccessCount++;
                summary.TotalNewBills += result.NewCount;
            }
            else
            {
                summary.FailedCount++;
            }

            summary.AccountResults.Add(result);

            OnProgressChanged(new SyncProgress
            {
                AccountId = account.AccountId,
                AccountName = account.AccountName,
                Status = result.Success ? "completed" : "failed",
                NewCount = result.NewCount,
                ErrorMessage = result.ErrorMessage,
            });
        }

        return summary;
    }

    /// <summary>
    /// 创建或恢复 EpayAuth 实例
    /// </summary>
    private async Task<EpayAuth> CreateEpayAuthAsync(
        string accountId,
        string password,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken)
    {
        var epayAuth = new EpayAuth(captchaResolver);

        // 尝试恢复会话
        var savedCookies = SessionInfoDb.GetDecryptedCookies(accountId);
        if (savedCookies != null)
        {
            // 测试会话是否仍然有效
            try
            {
                var probeResult = await epayAuth.ProbeLoginAsync(cancellationToken);
                if (probeResult is LoginProbe.AlreadyLoggedIn)
                {
                    return epayAuth;
                }
            }
            catch
            {
                // 探测失败，继续登录流程
            }

            SessionInfoDb.Invalidate(accountId);
        }

        // 需要重新登录
        var loginResult = await epayAuth.SubmitLoginAsync(accountId, password, cancellationToken);
        if (loginResult is not LoginSubmitResult.Success)
        {
            var errorMsg = loginResult switch
            {
                LoginSubmitResult.PasswordError => "密码错误",
                LoginSubmitResult.ValidateCodeError => "验证码错误",
                LoginSubmitResult.Failure f => f.Message,
                _ => "登录失败",
            };
            throw new InvalidOperationException($"CAS登录失败: {errorMsg}");
        }

        return epayAuth;
    }

    /// <summary>
    /// 保存会话信息
    /// </summary>
    private static async Task SaveSessionAsync(EpayAuth epayAuth, string accountId)
    {
        try
        {
            var isLoggedIn = await epayAuth.TestLoginStatusAsync();
            if (isLoggedIn)
            {
                // 获取 cookies 字符串
                var cookiesStr = epayAuth.HttpClient.HttpClient.DefaultRequestHeaders
                    .ToString();

                SessionInfoDb.Save(accountId, cookiesStr);
            }
        }
        catch
        {
            // 保存会话失败不影响同步结果
        }
    }

    private void OnProgressChanged(SyncProgress progress)
    {
        ProgressChanged?.Invoke(progress);
    }

    /// <summary>
    /// 数据库 BillStore 实现 — 供 shmtu-dotnet-lib 的 BillSync 使用
    /// </summary>
    private class DatabaseBillStore : IBillStore
    {
        private readonly string _accountId;
        private readonly int _identityId;
        private readonly HashSet<string> _existingNumbers;

        public DatabaseBillStore(string accountId, int identityId)
        {
            _accountId = accountId;
            _identityId = identityId;

            // 预加载已有交易号
            var existingBills = BillOriginalDb.GetAll(accountId);
            _existingNumbers = new HashSet<string>();
            foreach (var bill in existingBills)
            {
                if (!string.IsNullOrEmpty(bill.Number))
                    _existingNumbers.Add(bill.Number);
            }
        }

        public bool Contains(string number)
        {
            return _existingNumbers.Contains(number);
        }

        public void Merge(List<BillItemInfo> newBills)
        {
            if (newBills.Count == 0) return;

            var now = DateTime.Now.ToString("o");

            // 写入账号原始数据表
            var originalRecords = newBills.Select(b => BillOriginalDb.FromBillItemInfo(b, _accountId)).ToList();
            BillOriginalDb.InsertRange(_accountId, originalRecords);

            // 更新已有交易号缓存
            foreach (var bill in newBills)
            {
                if (!string.IsNullOrEmpty(bill.Number))
                    _existingNumbers.Add(bill.Number);
            }

            // 追加到身份合并数据表
            var mergedRecords = newBills.Select(b => BillMergedDb.FromBillItemInfo(b, _accountId)).ToList();
            BillMergedDb.AppendRange(_identityId, mergedRecords);
        }
    }
}
