using System;
using System.Collections.Generic;
using SqlSugar;

namespace shmtu.terminal.desktop.Models.User;

public class UserConfigure
{
    public static List<UserConfigure> UserConfigureList = [];

    public bool Enable { get; set; } = true;

    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)] public string Name { get; set; } = "";

    public bool EnableUpdate { get; set; } = true;

    public DateTime BirthDay { get; set; } = DateTime.MinValue;

    public List<AccountConfigure> AccountList { get; set; } = [];

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