using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source.UserData;

public class UserDbSource : BaseDbSource
{
    public UserDbSource(string userName)
    {
        DatabaseFileBaseName = $"user.{userName}.config";
    }
}