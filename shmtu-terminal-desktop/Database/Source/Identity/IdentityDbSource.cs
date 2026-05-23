using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source.Identity;

/// <summary>
/// 身份数据库连接 — 管理身份数据库文件
/// 对应数据库文件：Data/identity/<id>.sqlite
/// 包含表：bill_merged, operation_log
/// </summary>
public class IdentityDbSource : BaseDbSource
{
    private readonly int _identityId;

    public IdentityDbSource(int identityId)
    {
        _identityId = identityId;
        DatabaseFileBaseName = $"identity/{identityId}";
    }

    public int IdentityId => _identityId;
}
