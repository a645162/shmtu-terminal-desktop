using System.Collections.Generic;

namespace shmtu.terminal.desktop.Models.User;

public class UserModel
{
    public string Name = "";

    public int BirthYear = 0;
    public int BirthMonth = 0;
    public int BirthDay = 0;

    public List<string> AccountIdList = [];
    public List<AccountModel> AccountList = [];
}