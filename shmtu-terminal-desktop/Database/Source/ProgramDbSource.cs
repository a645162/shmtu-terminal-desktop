using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source;

/// <summary>
/// Common class for database operations
/// 数据库操作的公共类
/// </summary>
public class ProgramDbSource : BaseDbSource
{
    /// <summary>
    /// Database connection string
    /// 数据库连接字符串
    /// </summary>
    public new static readonly string Connection = "datasource=data/shmtu.terminal.sqlite";
}