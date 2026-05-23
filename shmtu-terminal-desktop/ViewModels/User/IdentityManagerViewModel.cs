using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Identity;
using shmtu.terminal.desktop.Services.Navigation;

namespace shmtu.terminal.desktop.ViewModels.User;

/// <summary>
/// Display wrapper for identity items in the ListBox
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
            this.RaisePropertyChanged(nameof(HasSelectedIdentity));
        }
    }

    public string SelectedIdentityName => SelectedIdentity?.Name ?? "";
    public bool HasSelectedIdentity => _selectedIdentity != null;

    // Identity commands
    public ReactiveCommand<Unit, Unit> AddIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> EditIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> SetDefaultIdentityCommand { get; }

    // Account commands — take AccountDisplayItem parameter directly
    public ReactiveCommand<Unit, Unit> AddAccountCommand { get; }
    public ReactiveCommand<AccountDisplayItem, Unit> EditAccountCommand { get; }
    public ReactiveCommand<AccountDisplayItem, Unit> DeleteAccountCommand { get; }

    public IdentityManagerViewModel()
    {
        // Identity commands
        AddIdentityCommand = ReactiveCommand.Create(AddIdentity);
        DeleteIdentityCommand = ReactiveCommand.Create(DeleteIdentity);
        EditIdentityCommand = ReactiveCommand.Create(EditIdentity);
        SetDefaultIdentityCommand = ReactiveCommand.Create(SetDefaultIdentity);

        // Account commands
        AddAccountCommand = ReactiveCommand.Create(AddAccount);
        EditAccountCommand = ReactiveCommand.Create<AccountDisplayItem>(EditAccount);
        DeleteAccountCommand = ReactiveCommand.Create<AccountDisplayItem>(DeleteAccount);

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
                Password = "",
                Enable = account.Enable,
                EnableUpdate = account.EnableUpdate,
                LastUpdateTime = account.LastUpdateTime,
            });
        }
    }

    private void AddIdentity()
    {
        var vm = new IdentityEditViewModel();
        vm.Saved += (_) =>
        {
            LoadIdentities();
            var newest = Identities.LastOrDefault();
            if (newest != null) SelectedIdentity = newest;
        };

        NavigationService.Instance.OpenWindow("IdentityEdit", vm);
    }

    private void EditIdentity()
    {
        if (_selectedIdentity == null) return;

        var vm = new IdentityEditViewModel(_selectedIdentity);
        vm.Saved += (_) =>
        {
            LoadIdentities();
            var updated = Identities.FirstOrDefault(i => i.Id == _selectedIdentity.Id);
            if (updated != null) SelectedIdentity = updated;
        };

        NavigationService.Instance.OpenWindow("IdentityEdit", vm);
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

    private void SetDefaultIdentity()
    {
        if (_selectedIdentity == null) return;

        IdentityDb.SetDefaultIdentity(_selectedIdentity.Id, true);

        foreach (var item in Identities)
        {
            item.IsDefault = item.Id == _selectedIdentity.Id;
        }

        Services.Config.TomlConfigService.UpdateAppConfig(config =>
        {
            config.Identity.RememberDefault = true;
            config.Identity.DefaultIdentityId = _selectedIdentity.Id;
        });
    }

    private void AddAccount()
    {
        if (_selectedIdentity == null) return;

        var vm = new AccountEditViewModel(_selectedIdentity.Id);
        vm.Saved += () =>
        {
            LoadIdentities(); // Refresh account counts
            LoadAccounts();
        };

        NavigationService.Instance.OpenWindow("AccountEdit", vm);
    }

    private void EditAccount(AccountDisplayItem account)
    {
        var vm = new AccountEditViewModel(account);
        vm.Saved += () =>
        {
            LoadAccounts();
        };

        NavigationService.Instance.OpenWindow("AccountEdit", vm);
    }

    private void DeleteAccount(AccountDisplayItem account)
    {
        try
        {
            AccountDb.Delete(account.Id);
            LoadIdentities(); // Refresh account counts
            LoadAccounts();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除账号失败: {ex.Message}";
        }
    }
}
