using shmtu.terminal.desktop.Database.Common;

namespace shmtu.terminal.desktop.Database.Source.UserData;

public class UserConfigureDbSource: BaseDbSource
{
    public UserConfigureDbSource()
    {
        DatabaseFileBaseName = $"user.config";
    }
}