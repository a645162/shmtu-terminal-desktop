using shmtu.terminal.desktop.Models.User;

namespace shmtu.terminal.desktop.ViewModels.User;

public class AccountManagerViewModel : ViewModelBase
{
    public string Greeting { get; set; } = "Welcome to Avalonia!";

    private readonly AccountModel _account;

    public AccountManagerViewModel()
    {
        _account = new AccountModel();
    }

    public AccountManagerViewModel(AccountModel account)
    {
        Greeting = "Greeting from AccountManagerViewModel";

        _account = account;
    }

    public string AccountId
    {
        get => _account.AccountId;
        set => _account.AccountId = value;
    }

    public string AccountName
    {
        get => _account.Name;
        set => _account.Name = value;
    }

    public string AccountPassword
    {
        get => _account.Password;
        set => _account.Password = value;
    }
}