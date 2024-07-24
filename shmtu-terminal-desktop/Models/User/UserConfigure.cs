using System;
using System.Collections.Generic;

namespace shmtu.terminal.desktop.Models.User;

public class UserConfigure
{
    public static List<UserConfigure> UserConfigureList = [];
    
    public bool Enable = true;

    public string Name = "";

    public bool EnableUpdate = true;

    public DateTime BirthDay = DateTime.MinValue;

    public List<AccountConfigure> AccountList = [];

    public void GenerateRandomAccount(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            AccountList.Add(AccountConfigure.GenerateRandomAccount());
        }
    }

    public static UserConfigure GenerateRandomUser()
    {
        var user = new UserConfigure
        {
            Name = "Test User",
            BirthDay = DateTime.Now
        };
        user.GenerateRandomAccount(3);
        return user;
    }
}