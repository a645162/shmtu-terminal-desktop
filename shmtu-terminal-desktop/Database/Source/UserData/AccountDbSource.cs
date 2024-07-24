using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source.UserData;

public class AccountDbSource : BaseDbSource
{
    public AccountDbSource(string accountId)
    {
        DatabaseFileBaseName = $"user.{accountId}.config";
    }
}