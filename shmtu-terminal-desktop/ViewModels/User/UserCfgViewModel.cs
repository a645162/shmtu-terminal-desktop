using System.Collections.Generic;
using System.Linq;
using shmtu.terminal.desktop.Models.User;

namespace shmtu.terminal.desktop.ViewModels.User;

public class UserCfgViewModel : ViewModelBase
{
    private List<string> UserNameList {
        get
        {
            return UserConfigure.UserConfigureList.Select(user => user.Name).ToList();
        }
    }

    // private UserConfigure? _currentUserConfigure;

    public UserCfgViewModel()
    {
        UserConfigure.UserConfigureList.Add(
            UserConfigure.GenerateRandomUser()
        );
        
        // _currentUserConfigure = UserConfigureList[0];
    }
}