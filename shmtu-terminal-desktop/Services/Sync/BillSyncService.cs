using System.Net;
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

    public BillSyncService() => _appConfig = TomlConfigService.LoadAppConfig();

    public event Action<SyncProgress>? ProgressChanged;

    public async Task<AccountSyncResult> SyncAccountAsync(
        AccountInfo account,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        if (!account.Enable || !account.EnableUpdate)
            return new AccountSyncResult { AccountId = account.AccountId, Success = true, NewCount = 0 };

        try
        {
            var password = AccountDb.GetDecryptedPassword(account.Id);
            InitDb.InitAccountDb(account.AccountId);
            InitDb.InitIdentityDb(account.IdentityId);
            var store = new DatabaseBillStore(account.AccountId, account.IdentityId);
            using var epayAuth = await CreateEpayAuthAsync(account.AccountId, password, captchaResolver, cancellationToken);

            var syncOptions = new SyncOptions
            {
                StartPage = 1,
                MaxPages = _appConfig.Sync.MaxPages,
                BillType = BillType.All,
                EarlyStopThreshold = _appConfig.Sync.EarlyStopThreshold,
            };

            var syncResult = await BillSync.IncrementalSyncAsync(epayAuth, store, syncOptions, cancellationToken);
            await SaveSessionAsync(epayAuth, account.AccountId);
            AccountDb.UpdateLastSyncTime(account.Id);

            return new AccountSyncResult { AccountId = account.AccountId, Success = true, NewCount = syncResult.NewCount };
        }
        catch (Exception ex)
        {
            return new AccountSyncResult { AccountId = account.AccountId, Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncSummary> SyncIdentityAsync(
        int identityId,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        return await SyncAccountsAsync(accounts, captchaResolver, cancellationToken);
    }

    public async Task<AccountSyncResult> FullUpdateAccountAsync(
        AccountInfo account,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken = default)
    {
        if (!account.Enable)
            return new AccountSyncResult { AccountId = account.AccountId };

        try
        {
            var password = AccountDb.GetDecryptedPassword(account.Id);
            InitDb.InitAccountDb(account.AccountId);
            InitDb.InitIdentityDb(account.IdentityId);
            var store = new DatabaseBillStore(account.AccountId, account.IdentityId);
            using var epayAuth = await CreateEpayAuthAsync(account.AccountId, password, captchaResolver, cancellationToken);

            var syncOptions = new SyncOptions
            {
                StartPage = 1, MaxPages = _appConfig.Sync.MaxPages,
                BillType = BillType.All, EarlyStopThreshold = int.MaxValue,
            };

            var syncResult = await BillSync.IncrementalSyncAsync(epayAuth, store, syncOptions, cancellationToken);
            OperationLogDb.ClearByAccountId(account.IdentityId, account.AccountId);
            await SaveSessionAsync(epayAuth, account.AccountId);
            AccountDb.UpdateLastSyncTime(account.Id);

            return new AccountSyncResult { AccountId = account.AccountId, Success = true, NewCount = syncResult.NewCount };
        }
        catch (Exception ex)
        {
            return new AccountSyncResult { AccountId = account.AccountId, Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SyncSummary> FullUpdateIdentityAsync(int identityId, ICaptchaResolver captchaResolver, CancellationToken cancellationToken = default)
    {
        var accounts = AccountDb.GetEnabledByIdentityId(identityId);
        var summary = await SyncAccountsAsync(accounts, captchaResolver, cancellationToken);
        OperationLogDb.ClearAll(identityId);
        return summary;
    }

    private async Task<SyncSummary> SyncAccountsAsync(List<AccountInfo> accounts, ICaptchaResolver captchaResolver, CancellationToken cancellationToken)
    {
        var summary = new SyncSummary { TotalAccounts = accounts.Count };

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

            if (result.Success) { summary.SuccessCount++; summary.TotalNewBills += result.NewCount; }
            else summary.FailedCount++;

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
    /// CRITICAL 3 fix: 从 SessionInfoDb 获取 cookies 并注入到 CasHttpClient 的 CookieContainer
    /// </summary>
    private async Task<EpayAuth> CreateEpayAuthAsync(
        string accountId,
        string password,
        ICaptchaResolver captchaResolver,
        CancellationToken cancellationToken)
    {
        var casClient = new CasHttpClient();

        // CRITICAL 3: 从 CookieContainer 注入会话
        var savedCookiesStr = SessionInfoDb.GetDecryptedCookies(accountId);
        if (!string.IsNullOrEmpty(savedCookiesStr))
        {
            RestoreCookiesToContainer(casClient.CookieContainer, savedCookiesStr, "ecard.shmtu.edu.cn");
        }

        var epayAuth = new EpayAuth(captchaResolver, casClient);

        // 尝试使用已有会话
        try
        {
            var probe = await epayAuth.ProbeLoginAsync(cancellationToken);
            if (probe is LoginProbe.AlreadyLoggedIn)
                return epayAuth;
        }
        catch { /* 探测失败，继续登录流程 */ }

        // 需要登录
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
            epayAuth.Dispose();
            throw new InvalidOperationException($"CAS登录失败: {errorMsg}");
        }

        return epayAuth;
    }

    /// <summary>
    /// CRITICAL 3: 将 cookies 字符串解析并注入到 CookieContainer
    /// 格式: name=value;name=value;...
    /// </summary>
    private static void RestoreCookiesToContainer(CookieContainer container, string cookiesStr, string domain)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(cookiesStr)) return;
            var cookies = cookiesStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var cookiePair in cookies)
            {
                var parts = cookiePair.Split('=', 2);
                if (parts.Length < 2) continue;
                var name = parts[0].Trim();
                var value = parts[1].Trim();
                if (string.IsNullOrEmpty(name)) continue;
                container.Add(new Cookie(name, value) { Domain = domain });
            }
        }
        catch { /* 忽略解析错误 */ }
    }

    /// <summary>
    /// CRITICAL 3 fix: 从 CookieContainer 提取 cookies 并保存（不是 DefaultRequestHeaders）
    /// </summary>
    private static async Task SaveSessionAsync(EpayAuth epayAuth, string accountId)
    {
        try
        {
            if (await epayAuth.TestLoginStatusAsync())
            {
                var cookies = DumpCookiesFromContainer(epayAuth.HttpClient.CookieContainer, "ecard.shmtu.edu.cn");
                if (!string.IsNullOrEmpty(cookies))
                    SessionInfoDb.Save(accountId, cookies);
            }
        }
        catch { /* 保存失败不影响同步 */ }
    }

    private static string DumpCookiesFromContainer(CookieContainer container, string domain)
    {
        try
        {
            var cookies = container.GetCookies(new Uri($"https://{domain}"));
            var parts = new List<string>();
            foreach (Cookie c in cookies)
                parts.Add($"{c.Name}={c.Value}");
            return string.Join("; ", parts);
        }
        catch { return ""; }
    }

    private void OnProgressChanged(SyncProgress progress) => ProgressChanged?.Invoke(progress);

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
        }

        public bool Contains(string number) => _existingNumbers.Contains(number);

        public void Merge(List<BillItemInfo> newBills)
        {
            if (newBills.Count == 0) return;

            var originalRecords = newBills.Select(b => BillOriginalDb.FromBillItemInfo(b, _accountId)).ToList();
            BillOriginalDb.InsertRange(_accountId, originalRecords);

            foreach (var bill in newBills)
                if (!string.IsNullOrEmpty(bill.Number)) _existingNumbers.Add(bill.Number);

            var mergedRecords = newBills.Select(b => BillMergedDb.FromBillItemInfo(b, _accountId)).ToList();
            BillMergedDb.AppendRange(_identityId, mergedRecords);
        }
    }
}
