using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source.Bill;

/// <summary>
/// 账号账单数据库连接 — 管理每个账号的独立数据库文件
/// 对应数据库文件：Data/account/<account_id>.sqlite
/// 包含表：bill_original
/// </summary>
public class BillOriginalDbSource : BaseDbSource
{
    private readonly string _accountId;

    public BillOriginalDbSource(string accountId)
    {
        _accountId = accountId;
        DatabaseFileBaseName = $"account/{accountId}";
    }

    public string AccountId => _accountId;
}
