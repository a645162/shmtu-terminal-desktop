using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Services.Security;

namespace shmtu.terminal.desktop.ViewModels.User;

/// <summary>
/// Display wrapper for identity items in the ListBox
/// Adds computed properties for UI display
/// </summary>
public class IdentityDisplayItem : ViewModelBase
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int AccountCount { get; set; }
    public bool IsDefault { get; set; }
    public bool Enable { get; set; }
    public bool EnableUpdate { get; set; }
}

/// <summary>
/// Display wrapper for account items in the cards list
/// </summary>
public class AccountDisplayItem : ViewModelBase
{
    public int Id { get; set; }
    public int IdentityId { get; set; }
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Password { get; set; } = "";

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

    public string LastUpdateTime { get; set; } = "";
}

/// <summary>
/// ViewModel for the Identity Manager window
/// XAML bindings: Identities, SelectedIdentity, AddIdentityCommand, DeleteIdentityCommand,
///   RenameIdentityCommand, SetDefaultIdentityCommand, Accounts, AddAccountCommand,
///   EditAccountCommand, DeleteAccountCommand, SetDefaultAccountCommand,
///   IsAccountSelected, SelectedAccount, SaveAccountCommand
/// </summary>
public class IdentityManagerViewModel : ViewModelBase
{
    public ObservableCollection<IdentityDisplayItem> Identities { get; } = [];
    public ObservableCollection<AccountDisplayItem> Accounts { get; } = [];

    private IdentityDisplayItem? _selectedIdentity;
    public IdentityDisplayItem? SelectedIdentity
    {
        get => _selectedIdentity;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedIdentity, value);
            LoadAccounts();
            this.RaisePropertyChanged(nameof(SelectedIdentityName));
        }
    }

    public string SelectedIdentityName => SelectedIdentity?.Name ?? "";

    private AccountDisplayItem? _selectedAccount;
    public AccountDisplayItem? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAccount, value);
            this.RaisePropertyChanged(nameof(IsAccountSelected));
        }
    }

    public bool IsAccountSelected => _selectedAccount != null;

    // Identity commands
    public ReactiveCommand<Unit, Unit> AddIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> SetDefaultIdentityCommand { get; }

    // Account commands
    public ReactiveCommand<Unit, Unit> AddAccountCommand { get; }
    public ReactiveCommand<Unit, Unit> EditAccountCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteAccountCommand { get; }
    public ReactiveCommand<Unit, Unit> SetDefaultAccountCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveAccountCommand { get; }

    public IdentityManagerViewModel()
    {
        // Identity commands
        AddIdentityCommand = ReactiveCommand.Create(AddIdentity);
        DeleteIdentityCommand = ReactiveCommand.Create(DeleteIdentity);
        RenameIdentityCommand = ReactiveCommand.Create(RenameIdentity);
        SetDefaultIdentityCommand = ReactiveCommand.Create(SetDefaultIdentity);

        // Account commands
        AddAccountCommand = ReactiveCommand.Create(AddAccount);
        EditAccountCommand = ReactiveCommand.Create(() => { /* SelectedAccount is already loaded for editing */ });
        DeleteAccountCommand = ReactiveCommand.Create(DeleteAccount);
        SetDefaultAccountCommand = ReactiveCommand.Create(SetDefaultAccount);
        SaveAccountCommand = ReactiveCommand.Create(SaveAccount);

        // Load initial data
        LoadIdentities();
    }

    private void LoadIdentities()
    {
        Identities.Clear();
        var identities = IdentityDb.GetAll();
        var defaultIdentity = IdentityDb.GetDefaultIdentity();

        foreach (var identity in identities)
        {
            var accountCount = AccountDb.GetByIdentityId(identity.Id).Count;
            Identities.Add(new IdentityDisplayItem
            {
                Id = identity.Id,
                Name = identity.Name,
                AccountCount = accountCount,
                IsDefault = defaultIdentity?.Id == identity.Id,
                Enable = identity.Enable,
                EnableUpdate = identity.EnableUpdate,
            });
        }
    }

    private void LoadAccounts()
    {
        Accounts.Clear();

        if (_selectedIdentity == null) return;

        var accounts = AccountDb.GetByIdentityId(_selectedIdentity.Id);
        foreach (var account in accounts)
        {
            Accounts.Add(new AccountDisplayItem
            {
                Id = account.Id,
                IdentityId = account.IdentityId,
                AccountId = account.AccountId,
                AccountName = account.AccountName,
                Password = "", // Don't expose encrypted password
                Enable = account.Enable,
                EnableUpdate = account.EnableUpdate,
                LastUpdateTime = account.LastUpdateTime,
            });
        }
    }

    private void AddIdentity()
    {
        var newIdentity = new IdentityInfo
        {
            Name = $"身份 {IdentityDb.GetAll().Count + 1}",
            Enable = true,
            EnableUpdate = true,
        };

        var id = IdentityDb.Add(newIdentity);
        LoadIdentities();

        // Select the newly added identity
        var newItem = Identities.FirstOrDefault(i => i.Id == id);
        if (newItem != null)
            SelectedIdentity = newItem;
    }

    private void DeleteIdentity()
    {
        if (_selectedIdentity == null) return;

        try
        {
            IdentityDb.Delete(_selectedIdentity.Id);
            LoadIdentities();
            SelectedIdentity = Identities.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除身份失败: {ex.Message}";
        }
    }

    private void RenameIdentity()
    {
        if (_selectedIdentity == null) return;

        // TODO: Show rename dialog
        // For now, just update the name in the database
        var identity = IdentityDb.GetById(_selectedIdentity.Id);
        if (identity != null)
        {
            identity.Name = _selectedIdentity.Name;
            IdentityDb.Update(identity);
            LoadIdentities();
        }
    }

    private void SetDefaultIdentity()
    {
        if (_selectedIdentity == null) return;

        IdentityDb.SetDefaultIdentity(_selectedIdentity.Id, true);

        // Update display
        foreach (var item in Identities)
        {
            item.IsDefault = item.Id == _selectedIdentity.Id;
        }

        // Update config
        Services.Config.TomlConfigService.UpdateAppConfig(config =>
        {
            config.Identity.RememberDefault = true;
            config.Identity.DefaultIdentityId = _selectedIdentity.Id;
        });
    }

    private void AddAccount()
    {
        if (_selectedIdentity == null) return;

        // TODO: Show add account dialog
        // For now, create a placeholder account
        var newAccount = new AccountInfo
        {
            IdentityId = _selectedIdentity.Id,
            AccountName = "新账号",
            AccountId = "000000000000",
            Enable = true,
            EnableUpdate = true,
        };

        try
        {
            AccountDb.Add(newAccount, "default_password");
            LoadAccounts();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"添加账号失败: {ex.Message}";
        }
    }

    private void DeleteAccount()
    {
        if (_selectedAccount == null) return;

        try
        {
            AccountDb.Delete(_selectedAccount.Id);
            LoadAccounts();
            SelectedAccount = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除账号失败: {ex.Message}";
        }
    }

    private void SetDefaultAccount()
    {
        // The concept of "default account" maps to the first enabled account
        // For now, just ensure the account is enabled
        if (_selectedAccount == null) return;
        _selectedAccount.Enable = true;
    }

    private void SaveAccount()
    {
        if (_selectedAccount == null) return;

        try
        {
            var account = AccountDb.GetById(_selectedAccount.Id);
            if (account == null) return;

            account.AccountName = _selectedAccount.AccountName;
            account.AccountId = _selectedAccount.AccountId;
            account.Enable = _selectedAccount.Enable;
            account.EnableUpdate = _selectedAccount.EnableUpdate;

            AccountDb.Update(account);

            // If password was changed
            if (!string.IsNullOrEmpty(_selectedAccount.Password))
            {
                AccountDb.UpdatePassword(_selectedAccount.Id, _selectedAccount.Password);
            }

            LoadAccounts();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存账号失败: {ex.Message}";
        }
    }
}
