using System;

namespace shmtu.terminal.desktop.Models.User;

public class AccountModel
{
    public bool Enable = true;

    public string AccountId = "";
    public string Name = "";
    public string Password = "";

    public bool EnableUpdate = true;

    // Account Expire Date
    public DateTime ExpireDate = DateTime.MaxValue;

    public AccountModel Clone()
    {
        return new AccountModel
        {
            Enable = Enable,
            AccountId = AccountId,
            Name = Name,
            Password = Password,
            EnableUpdate = EnableUpdate,
            ExpireDate = ExpireDate
        };
    }

    public bool CheckIsExpired()
    {
        return DateTime.Now > ExpireDate;
    }

    public bool CheckIsNeedUpdate()
    {
        if (Enable && EnableUpdate)
        {
            return CheckIsExpired();
        }

        return false;
    }
}