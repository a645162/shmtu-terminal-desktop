using System.IO;
using shmtu.terminal.desktop.Database.Manage.User;

namespace shmtu.terminal.desktop.Database;

public static class InitDb
{
    public static void Init()
    {
        // Create Directory

        const string dirPath = "./Data/";
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        // Load Static Data
        UserConfigureDb.Init();
    }
}