using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;

namespace shmtu.terminal.desktop.ViewModels.User;

public class AccountEditViewModel : ViewModelBase
{
    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => this.RaiseAndSetIfChanged(ref _isEditMode, value);
    }

    public string WindowTitle => IsEditMode ? "编辑账号" : "新建账号";

    private int _identityId;

    private string _accountName = "";
    public string AccountName
    {
        get => _accountName;
        set => this.RaiseAndSetIfChanged(ref _accountName, value);
    }

    private string _accountId = "";
    public string AccountId
    {
        get => _accountId;
        set => this.RaiseAndSetIfChanged(ref _accountId, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    private string _passwordHint = "";
    public string PasswordHint
    {
        get => _passwordHint;
        set => this.RaiseAndSetIfChanged(ref _passwordHint, value);
    }

    private bool _enable = true;
    public bool Enable
    {
        get => _enable;
        set => this.RaiseAndSetIfChanged(ref _enable, value);
    }

    private bool _enableUpdate = true;
    public bool EnableUpdate
    {
        get => _enableUpdate;
        set => this.RaiseAndSetIfChanged(ref _enableUpdate, value);
    }

    private string _expireDate = "2099-12-31";
    public string ExpireDate
    {
        get => _expireDate;
        set => this.RaiseAndSetIfChanged(ref _expireDate, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>
    /// Raised when user confirms save.
    /// </summary>
    public event Action? Saved;

    /// <summary>
    /// Raised when user cancels.
    /// </summary>
    public event Action? Cancelled;

    private readonly int _existingAccountId;

    /// <summary>
    /// Create mode: new account under the given identity
    /// </summary>
    public AccountEditViewModel(int identityId)
    {
        _identityId = identityId;
        IsEditMode = false;
        PasswordHint = "输入密码";

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => Cancelled?.Invoke());
    }

    /// <summary>
    /// Edit mode: edit existing account
    /// </summary>
    public AccountEditViewModel(AccountDisplayItem existing) : this()
    {
        _existingAccountId = existing.Id;
        _identityId = existing.IdentityId;
        IsEditMode = true;
        AccountName = existing.AccountName;
        AccountId = existing.AccountId;
        Password = "";
        PasswordHint = "留空则不修改密码";
        Enable = existing.Enable;
        EnableUpdate = existing.EnableUpdate;

        var account = AccountDb.GetById(existing.Id);
        if (account != null)
        {
            ExpireDate = account.ExpireDate;
        }
    }

    private AccountEditViewModel()
    {
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => Cancelled?.Invoke());
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(AccountName))
        {
            ErrorMessage = "账号名称不能为空";
            return;
        }

        if (!IsEditMode)
        {
            // Validate student ID for new accounts
            if (string.IsNullOrWhiteSpace(AccountId) || AccountId.Length != 12 || !AccountId.All(char.IsDigit))
            {
                ErrorMessage = "学号必须为12位数字";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "密码不能为空";
                return;
            }
        }

        try
        {
            if (IsEditMode)
            {
                var account = AccountDb.GetById(_existingAccountId);
                if (account == null) return;

                account.AccountName = AccountName.Trim();
                account.AccountId = AccountId.Trim();
                account.Enable = Enable;
                account.EnableUpdate = EnableUpdate;
                account.ExpireDate = ExpireDate;

                AccountDb.Update(account);

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    AccountDb.UpdatePassword(_existingAccountId, Password);
                }
            }
            else
            {
                var newAccount = new Models.Identity.AccountInfo
                {
                    IdentityId = _identityId,
                    AccountName = AccountName.Trim(),
                    AccountId = AccountId.Trim(),
                    Enable = Enable,
                    EnableUpdate = EnableUpdate,
                    ExpireDate = ExpireDate,
                };

                AccountDb.Add(newAccount, Password);
            }

            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
    }
}
