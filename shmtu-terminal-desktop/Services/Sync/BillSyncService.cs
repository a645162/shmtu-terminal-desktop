using System.Net;
using shmtu.terminal.desktop.Services.Security;
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

namespace shmtu.terminal.desktop.Services.Sync;

public class SyncProgress
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int NewCount { get; set; }
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }
}

public class SyncSummary
{
    public int TotalAccounts { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalNewBills { get; set; }
    public List<AccountSyncResult> AccountResults { get; set; } = [];
}

public class AccountSyncResult
{
    public string AccountId { get; set; } = "";
    public bool Success { get; set; }
    public int NewCount { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BillSyncService
{
    private readonly AppConfig _appConfig;

    public BillSyncService()
    {
        LoggingService.Debug("[BillSyncService] BillSyncService 实例创建");
        _appConfig = TomlConfigService.LoadAppConfig();
        LoggingService.Debug("[BillSyncService] 已加载应用配置 | MaxPages={Max} | EarlyStop={Early}",
            _appConfig.Sync.MaxPages, _appConfig.Sync.EarlyStopThreshold);
    }

    public event Action<SyncProgress>? ProgressChanged;

    public async Task<AccountSyncResult> SyncAccountAsync(
        AccountInfo account,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        LoggingService.Information("[BillSync] 开始同步账号 | AccountId={AccountId} | Name={Name} | EnableUpdate={Enable}",
            account.AccountId, account.AccountName, account.EnableUpdate);

        if (!account.Enable || !account.EnableUpdate)
        {
            LoggingService.Debug("[BillSync] 账号已禁用或不允许更新，跳过 | AccountId={AccountId}", account.AccountId);
            return new AccountSyncResult { AccountId = account.AccountId, Success = true, NewCount = 0 };
        }

        try
        {
            var password = AccountDb.GetDecryptedPassword(account.Id);
            LoggingService.Debug("[BillSync] 获取账号密码成功 | AccountId={AccountId}", account.AccountId);

            LoggingService.Debug("[BillSync] 初始化数据库 | AccountId={AccountId} | IdentityId={IdentityId}",
                account.AccountId, account.IdentityId);
            InitDb.InitAccountDb(account.AccountId);
            InitDb.InitIdentityDb(account.IdentityId);

            var store = new DatabaseBillStore(account.AccountId, account.IdentityId);
            LoggingService.Debug("[BillSync] 创建 DatabaseBillStore | ExistingCount={Count}", store.GetExistingCount());

            LoggingService.Information("[BillSync] 创建 EpayAuth 会话 | AccountId={AccountId}", account.AccountId);
            using var epayAuth = await CreateEpayAuthAsync(account.AccountId, password, captchaResolver, cancellationToken);
            LoggingService.Information("[BillSync] EpayAuth 会话创建成功 | AccountId={AccountId}", account.AccountId);

            var syncOptions = new SyncOptions
            {
                StartPage = 1,
                MaxPages = _appConfig.Sync.MaxPages,
                BillType = BillType.All,
                EarlyStopThreshold = _appConfig.Sync.EarlyStopThreshold,
            };
            LoggingService.Information("[BillSync] 开始增量同步 | MaxPages={Max} | EarlyStop={Early}",
                syncOptions.MaxPages, syncOptions.EarlyStopThreshold);

            var syncResult = await BillSync.IncrementalSyncAsync(epayAuth, store, syncOptions, cancellationToken);
            LoggingService.Information("[BillSync] 增量同步完成 | AccountId={AccountId} | NewCount={New} ",
                account.AccountId, syncResult.NewCount);

            LoggingService.Debug("[BillSync] 保存会话信息 | AccountId={AccountId}", account.AccountId);
            await SaveSessionAsync(epayAuth, account.AccountId);

            LoggingService.Debug("[BillSync] 更新最后同步时间 | AccountId={AccountId}", account.AccountId);
            AccountDb.UpdateLastSyncTime(account.Id);

            return new AccountSyncResult { AccountId = account.AccountId, Success = true, NewCount = syncResult.NewCount };
        }
        catch (Exception ex)
        {
            LoggingService.Fatal(ex, "[BillSync] 致命错误：账号同步失败 | AccountId={AccountId}",
                account.AccountId, ex.Message);
            if (ex.InnerException is System.Security.Cryptography.CryptographicException || ex is Services.Security.DecryptionFailedException)
            {
                LoggingService.Fatal("[BillSync] 检测到解密失败，正在清除所有加密数据...");
                Services.Security.EncryptionService.ClearAllData();
            }
            return new AccountSyncResult { AccountId = account.AccountId, Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncSummary> SyncIdentityAsync(
        int identityId,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        LoggingService.Information("[BillSync] 开始同步身份下的所有账号 | IdentityId={IdentityId}", identityId);
        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        LoggingService.Information("[BillSync] 找到 {Count} 个已启用的账号", accounts.Count);
        var result = await SyncAccountsAsync(accounts, captchaResolver, cancellationToken);
        LoggingService.Information("[BillSync] 身份同步完成 | IdentityId={IdentityId} | Success={S} | Failed={F} | NewTotal={New}",
            identityId, result.SuccessCount, result.FailedCount, result.TotalNewBills);
        return result;
    }

    public async Task<AccountSyncResult> FullUpdateAccountAsync(
        AccountInfo account,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        LoggingService.Information("[BillSync] 开始完整更新账号 | AccountId={AccountId} | Name={Name}",
            account.AccountId, account.AccountName);

        if (!account.Enable)
        {
            LoggingService.Debug("[BillSync] 账号已禁用，跳过 | AccountId={AccountId}", account.AccountId);
            return new AccountSyncResult { AccountId = account.AccountId };
        }

        try
        {
            var password = AccountDb.GetDecryptedPassword(account.Id);
            LoggingService.Debug("[BillSync] 获取账号密码成功");

            LoggingService.Debug("[BillSync] 初始化数据库");
            InitDb.InitAccountDb(account.AccountId);
            InitDb.InitIdentityDb(account.IdentityId);

            var store = new DatabaseBillStore(account.AccountId, account.IdentityId);
            LoggingService.Debug("[BillSync] 创建 DatabaseBillStore | ExistingCount={Count}", store.GetExistingCount());

            LoggingService.Information("[BillSync] 创建 EpayAuth 会话（完整更新模式）");
            using var epayAuth = await CreateEpayAuthAsync(account.AccountId, password, captchaResolver, cancellationToken);

            var syncOptions = new SyncOptions
            {
                StartPage = 1, MaxPages = _appConfig.Sync.MaxPages,
                BillType = BillType.All, EarlyStopThreshold = int.MaxValue,
            };
            LoggingService.Information("[BillSync] 开始完整同步（禁用提前停止）");

            var syncResult = await BillSync.IncrementalSyncAsync(epayAuth, store, syncOptions, cancellationToken);
            LoggingService.Information("[BillSync] 完整同步完成 | NewCount={New} | Total={Total}",
                syncResult.NewCount, syncResult.PagesFetched);

            LoggingService.Debug("[BillSync] 清除操作日志 | AccountId={AccountId}", account.AccountId);
            OperationLogDb.ClearByAccountId(account.IdentityId, account.AccountId);

            await SaveSessionAsync(epayAuth, account.AccountId);
            AccountDb.UpdateLastSyncTime(account.Id);

            return new AccountSyncResult { AccountId = account.AccountId, Success = true, NewCount = syncResult.NewCount };
        }
        catch (Exception ex)
        {
            LoggingService.Error(ex, "[BillSync] 完整更新失败 | AccountId={AccountId}", account.AccountId);
            if (ex.InnerException is System.Security.Cryptography.CryptographicException || ex is Services.Security.DecryptionFailedException)
            {
                LoggingService.Fatal("[BillSync] 检测到解密失败，正在清除所有加密数据...");
                Services.Security.EncryptionService.ClearAllData();
            }
            return new AccountSyncResult { AccountId = account.AccountId, Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncSummary> FullUpdateIdentityAsync(int identityId, ICaptchaResolver captchaResolver, CancellationToken cancellationToken = default)
    {
        LoggingService.Information("[BillSync] 开始完整更新身份 | IdentityId={IdentityId}", identityId);
        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        var summary = await SyncAccountsAsync(accounts, captchaResolver, cancellationToken);
        LoggingService.Information("[BillSync] 清除身份所有操作日志 | IdentityId={IdentityId}", identityId);
        OperationLogDb.ClearAll(identityId);
        LoggingService.Information("[BillSync] 完整更新完成 | Success={S} | Failed={F}", summary.SuccessCount, summary.FailedCount);
        return summary;
    }

    private async Task<SyncSummary> SyncAccountsAsync(List<AccountInfo> accounts, ICaptchaResolver captchaResolver, CancellationToken cancellationToken)
    {
        var summary = new SyncSummary { TotalAccounts = accounts.Count };
        LoggingService.Information("[BillSync] 开始批量同步 {Count} 个账号", accounts.Count);

        for (int i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            LoggingService.Debug("[BillSync] 进度: {Current}/{Total} | AccountId={AccountId}",
                i + 1, accounts.Count, account.AccountId);

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
                LoggingService.Information("[BillSync] 账号同步成功 | AccountId={AccountId} | NewCount={New}",
                    account.AccountId, result.NewCount);
            }
            else
            {
                summary.FailedCount++;
                LoggingService.Warning("[BillSync] 账号同步失败 | AccountId={AccountId} | Error={Error}",
                    account.AccountId, result.ErrorMessage);
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

        LoggingService.Information("[BillSync] 批量同步完成 | Total={Total} | Success={S} | Failed={F} | NewBills={New}",
            summary.TotalAccounts, summary.SuccessCount, summary.FailedCount, summary.TotalNewBills);
        return summary;
    }

    private async Task<EpayAuth> CreateEpayAuthAsync(
        string accountId,
        string password,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken)
    {
        LoggingService.Debug("[BillSync] 创建 CAS 客户端 | AccountId={AccountId}", accountId);
        var casClient = new CasHttpClient();

        var savedCookiesStr = SessionInfoDb.GetDecryptedCookies(accountId);
        if (!string.IsNullOrEmpty(savedCookiesStr))
        {
            LoggingService.Debug("[BillSync] 发现保存的会话，尝试恢复 | AccountId={AccountId}", accountId);
            RestoreCookiesToContainer(casClient.CookieContainer, savedCookiesStr);
        }
        else
        {
            LoggingService.Debug("[BillSync] 无保存的会话，将执行新登录 | AccountId={AccountId}", accountId);
        }

        var epayAuth = new EpayAuth(captchaResolver, casClient);

        try
        {
            LoggingService.Debug("[BillSync] 探测登录状态 | AccountId={AccountId}", accountId);
            var probe = await epayAuth.ProbeLoginAsync(cancellationToken);
            if (probe is LoginProbe.AlreadyLoggedIn)
            {
                LoggingService.Information("[BillSync] 会话复用成功，已登录 | AccountId={AccountId}", accountId);
                return epayAuth;
            }
            LoggingService.Debug("[BillSync] 会话已过期，需要重新登录 | AccountId={AccountId}", accountId);
        }
        catch (Exception ex)
        {
            LoggingService.Debug("[BillSync] 探测登录状态失败，将执行新登录 | Error={Error}", ex.Message);
        }

        LoggingService.Information("[BillSync] 执行新登录 | AccountId={AccountId}", accountId);
        var result = await epayAuth.SubmitLoginAsync(accountId, password, cancellationToken);
        if (result is not LoginSubmitResult.Success)
        {
            var errorMsg = result switch
            {
                LoginSubmitResult.PasswordError => "密码错误",
                LoginSubmitResult.ValidateCodeError => "验证码错误",
                LoginSubmitResult.Failure f => f.Message,
                _ => "登录失败",
            };
            LoggingService.Error("[BillSync] CAS 登录失败 | AccountId={AccountId} | Error={Error}", accountId, errorMsg);
            epayAuth.Dispose();
            throw new InvalidOperationException($"CAS登录失败: {errorMsg}");
        }

        LoggingService.Information("[BillSync] CAS 登录成功 | AccountId={AccountId}", accountId);
        return epayAuth;
    }

    private static void RestoreCookiesToContainer(CookieContainer container, string cookiesStr)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cookiesStr)) return;

            var cookieParts = cookiesStr.Split(new[] { ';', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var count = 0;
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
                    count++;
                    LoggingService.Verbose("[BillSync] 添加 Cookie | Name={Name}", name);
                }
                catch
                {
                    try { container.Add(new Cookie(name, value)); count++; }
                    catch { }
                }
            }
            LoggingService.Debug("[BillSync] 已恢复 {Count} 个 Cookie | Domain={Domain}", count, domain);
        }
        catch (Exception ex)
        {
            LoggingService.Warning("[BillSync] 恢复 Cookie 失败，继续使用新登录 | Error={Error}", ex.Message);
        }
    }

    private static async Task SaveSessionAsync(EpayAuth epayAuth, string accountId)
    {
        try
        {
            LoggingService.Debug("[BillSync] 测试登录状态并保存会话 | AccountId={AccountId}", accountId);
            if (await epayAuth.TestLoginStatusAsync())
            {
                var cookies = DumpAllDomainCookies(epayAuth.HttpClient.CookieContainer);
                if (!string.IsNullOrEmpty(cookies))
                {
                    SessionInfoDb.Save(accountId, cookies);
                    LoggingService.Debug("[BillSync] 会话已保存 | AccountId={AccountId} | Count={Count}",
                        accountId, cookies.Split(';', StringSplitOptions.RemoveEmptyEntries).Length);
                }
            }
        }
        catch (Exception ex)
        {
            LoggingService.Warning("[BillSync] 保存会话失败（非致命） | Error={Error}", ex.Message);
        }
    }

    private static string DumpAllDomainCookies(CookieContainer container)
    {
        var parts = new List<string>();
        var domains = new[] { ".shmtu.edu.cn", "shmtu.edu.cn", "cas.shmtu.edu.cn", "ecard.shmtu.edu.cn" };

        foreach (var domain in domains)
        {
            try
            {
                var cookies = container.GetCookies(new Uri($"https://{domain}"));
                foreach (Cookie c in cookies)
                    parts.Add($"{c.Name}={c.Value}");
            }
            catch { }
        }

        var result = string.Join("; ", parts.Distinct());
        LoggingService.Verbose("[BillSync] 提取所有 Cookie | Count={Count}", parts.Count);
        return result;
    }

    private void OnProgressChanged(SyncProgress progress)
    {
        LoggingService.Debug("[BillSync] 进度更新 | AccountId={AccountId} | Status={Status} | NewCount={New}",
            progress.AccountId, progress.Status, progress.NewCount);
        ProgressChanged?.Invoke(progress);
    }

    private class DatabaseBillStore : IBillStore
    {
        private readonly string _accountId;
        private readonly int _identityId;
        private readonly HashSet<string> _existingNumbers;

        public DatabaseBillStore(string accountId, int identityId)
        {
            _accountId = accountId;
            _identityId = identityId;
            _existingNumbers = new HashSet<string>(
                BillOriginalDb.GetAll(accountId)
                    .Where(b => !string.IsNullOrEmpty(b.Number))
                    .Select(b => b.Number!));
            LoggingService.Debug("[DatabaseBillStore] 初始化 | AccountId={AccountId} | ExistingCount={Count}",
                accountId, _existingNumbers.Count);
        }

        public int GetExistingCount() => _existingNumbers.Count;

        public bool Contains(string number)
        {
            var exists = _existingNumbers.Contains(number);
            LoggingService.Verbose("[DatabaseBillStore] Contains | Number={Number} | Exists={Exists}", number, exists);
            return exists;
        }

        public void Merge(List<BillItemInfo> newBills)
        {
            if (newBills.Count == 0)
            {
                LoggingService.Verbose("[DatabaseBillStore] 无新账单需要合并");
                return;
            }

            LoggingService.Debug("[DatabaseBillStore] 开始合并 {Count} 条新账单", newBills.Count);

            var originalRecords = newBills.Select(b => BillOriginalDb.FromBillItemInfo(b, _accountId)).ToList();
            BillOriginalDb.InsertRange(_accountId, originalRecords);
            LoggingService.Debug("[DatabaseBillStore] 原始账单已写入 | Count={Count}", originalRecords.Count);

            foreach (var bill in newBills)
                if (!string.IsNullOrEmpty(bill.Number)) _existingNumbers.Add(bill.Number);

            var mergedRecords = newBills.Select(b => BillMergedDb.FromBillItemInfo(b, _accountId)).ToList();
            BillMergedDb.AppendRange(_identityId, mergedRecords);
            LoggingService.Debug("[DatabaseBillStore] 合并账单已写入 | Count={Count}", mergedRecords.Count);
        }
    }
}