using System;

namespace shmtu.terminal.desktop.Models.User;

public class AccountConfigure
{
    public bool Enable = true;

    public string AccountId = "";
    public string Name = "";
    public string Password = "";

    public bool EnableUpdate = true;

    // Account Expire Date
    public DateTime ExpireDate = DateTime.MaxValue;
    
    public DateTime LastUpdateTime = DateTime.MinValue;

    public AccountConfigure Clone()
    {
        return new AccountConfigure
        {
            Enable = Enable,
            AccountId = AccountId,
            Name = Name,
            Password = Password,
            EnableUpdate = EnableUpdate,
            ExpireDate = ExpireDate
        };
    }

    public static AccountConfigure GenerateRandomAccount()
    {
        return new AccountConfigure
        {
            AccountId = "202312312345",
            Name = "Test User",
            Password = "123456"
        };
    }

    public bool CheckIsHaveError()
    {
        if (string.IsNullOrEmpty(AccountId))
        {
            return true;
        }

        // 202312312345
        if (AccountId.Length != 12)
        {
            return true;
        }

        return false;
    }

    public bool IsCorrect()
    {
        return !CheckIsHaveError();
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