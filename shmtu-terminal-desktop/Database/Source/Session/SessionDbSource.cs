using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source.Session;

/// <summary>
/// 会话数据库连接 — 管理 cookies 等会话信息
/// 对应数据库文件：Data/session.sqlite
/// 包含表：session_info
/// </summary>
public class SessionDbSource : BaseDbSource
{
    public SessionDbSource()
    {
        DatabaseFileBaseName = "session";
    }
}
