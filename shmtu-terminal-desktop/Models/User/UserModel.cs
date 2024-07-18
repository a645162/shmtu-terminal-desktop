using System;
using System.Collections.Generic;

namespace shmtu.terminal.desktop.Models.User;

public class UserModel
{
    public bool Enable = true;

    public string Name = "";

    public bool EnableUpdate = true;

    public DateTime BirthDay = DateTime.MinValue;

    public List<AccountModel> AccountList = [];

    public void GenerateRandomAccount(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            AccountList.Add(AccountModel.GenerateRandomAccount());
        }
    }

    public static UserModel GenerateRandomUser()
    {
        var user = new UserModel
        {
            Name = "Test User",
            BirthDay = DateTime.Now
        };
        user.GenerateRandomAccount(3);
        return user;
    }
}