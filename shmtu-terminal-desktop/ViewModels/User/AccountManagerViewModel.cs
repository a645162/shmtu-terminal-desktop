using shmtu.terminal.desktop.Models.User;

namespace shmtu.terminal.desktop.ViewModels.User;

public class AccountManagerViewModel : ViewModelBase
{
    public string Greeting { get; set; } = "Welcome to Avalonia!";

    private readonly AccountConfigure _accountConfigure;

    public AccountManagerViewModel()
    {
        _accountConfigure = new AccountConfigure();
    }

    public AccountManagerViewModel(AccountConfigure accountConfigure)
    {
        Greeting = "Greeting from AccountManagerViewModel";

        _accountConfigure = accountConfigure;
    }

    public string AccountId
    {
        get => _accountConfigure.AccountId;
        set => _accountConfigure.AccountId = value;
    }

    public string AccountName
    {
        get => _accountConfigure.Name;
        set => _accountConfigure.Name = value;
    }

    public string AccountPassword
    {
        get => _accountConfigure.Password;
        set => _accountConfigure.Password = value;
    }
}