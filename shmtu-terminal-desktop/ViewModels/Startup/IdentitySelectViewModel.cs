using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Services.Config;

namespace shmtu.terminal.desktop.ViewModels.Startup;

/// <summary>
/// Display wrapper for identity selection items
/// XAML bindings: Name, AccountCount, LastSyncInfo, IsSelected
/// </summary>
public class IdentitySelectItem : ViewModelBase
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int AccountCount { get; set; }
    public string LastSyncInfo { get; set; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

/// <summary>
/// ViewModel for the Identity Selection window at startup
/// XAML bindings: Identities, RememberDefault, ManageIdentityCommand, EnterCommand
/// </summary>
public class IdentitySelectViewModel : ViewModelBase
{
    public ObservableCollection<IdentitySelectItem> Identities { get; } = [];

    private bool _rememberDefault;
    public bool RememberDefault
    {
        get => _rememberDefault;
        set => this.RaiseAndSetIfChanged(ref _rememberDefault, value);
    }

    public ReactiveCommand<Unit, Unit> ManageIdentityCommand { get; }
    public ReactiveCommand<Unit, Unit> EnterCommand { get; }

    /// <summary>
    /// Event raised when user confirms identity selection
    /// </summary>
    public event Action<int>? IdentitySelected;

    public IdentitySelectViewModel()
    {
        ManageIdentityCommand = ReactiveCommand.Create(() =>
        {
            Services.Navigation.NavigationService.Instance.OpenWindow("IdentityManager",
                new ViewModels.User.IdentityManagerViewModel());
        });

        EnterCommand = ReactiveCommand.Create(OnEnter);

        LoadIdentities();
    }

    private void LoadIdentities()
    {
        Identities.Clear();
        var config = TomlConfigService.LoadAppConfig();
        var identities = IdentityDb.GetEnabled();
        var defaultIdentity = IdentityDb.GetDefaultIdentity();

        foreach (var identity in identities)
        {
            var accounts = AccountDb.GetByIdentityId(identity.Id);
            var lastSync = accounts.Count > 0
                ? accounts.Max(a => a.LastUpdateTime)
                : "";
            var syncInfo = string.IsNullOrEmpty(lastSync) ? "从未同步" : $"最后同步: {lastSync[..Math.Min(19, lastSync.Length)]}";

            var item = new IdentitySelectItem
            {
                Id = identity.Id,
                Name = identity.Name,
                AccountCount = accounts.Count,
                LastSyncInfo = syncInfo,
                IsSelected = config.Identity.RememberDefault && defaultIdentity?.Id == identity.Id,
            };

            Identities.Add(item);
        }

        RememberDefault = config.Identity.RememberDefault;

        // Auto-select first if only one identity
        if (Identities.Count == 1)
        {
            Identities[0].IsSelected = true;
        }
    }

    private void OnEnter()
    {
        var selectedItem = Identities.FirstOrDefault(i => i.IsSelected);
        if (selectedItem == null)
        {
            ErrorMessage = "请选择一个身份";
            return;
        }

        // Save remember default preference
        if (RememberDefault)
        {
            TomlConfigService.UpdateAppConfig(config =>
            {
                config.Identity.RememberDefault = true;
                config.Identity.DefaultIdentityId = selectedItem.Id;
            });
            IdentityDb.SetDefaultIdentity(selectedItem.Id, true);
        }
        else
        {
            TomlConfigService.UpdateAppConfig(config =>
            {
                config.Identity.RememberDefault = false;
            });
        }

        IdentitySelected?.Invoke(selectedItem.Id);
    }

    /// <summary>
    /// Get the selected identity ID, or 0 if none selected
    /// </summary>
    public int GetSelectedIdentityId()
    {
        return Identities.FirstOrDefault(i => i.IsSelected)?.Id ?? 0;
    }
}
