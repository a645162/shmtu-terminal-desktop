using shmtu.terminal.desktop.Database.Source.UserData;
using shmtu.terminal.desktop.Models.User;

namespace shmtu.terminal.desktop.Database.Manage.User;

public class UserConfigureDb
{
    // UserConfigure.
    public static void SaveUserConfigure()
    {
        var db = new UserConfigureDbSource().GetNewDbObj();
        // UserConfigure.UserConfigureList.ForEach(db.Save);
    }
}