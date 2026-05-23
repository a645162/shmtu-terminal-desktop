using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database.Manage.Identity;
using shmtu.terminal.desktop.Models.Identity;

namespace shmtu.terminal.desktop.ViewModels.User;

public class IdentityEditViewModel : ViewModelBase
{
    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set => this.RaiseAndSetIfChanged(ref _isEditMode, value);
    }

    public string WindowTitle => IsEditMode ? "编辑身份" : "新建身份";

    private string _name = "";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
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

    private string _birthday = "";
    public string Birthday
    {
        get => _birthday;
        set => this.RaiseAndSetIfChanged(ref _birthday, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    /// <summary>
    /// Raised when user confirms save. Parameter is the identity ID (0 for new).
    /// </summary>
    public event Action<int>? Saved;

    /// <summary>
    /// Raised when user cancels.
    /// </summary>
    public event Action? Cancelled;

    private readonly int _identityId;

    public IdentityEditViewModel()
    {
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => Cancelled?.Invoke());
    }

    public IdentityEditViewModel(IdentityDisplayItem existing) : this()
    {
        _identityId = existing.Id;
        IsEditMode = true;
        Name = existing.Name;
        Enable = existing.Enable;
        EnableUpdate = existing.EnableUpdate;

        var identity = IdentityDb.GetById(existing.Id);
        Birthday = identity?.Birthday ?? "";
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "身份名称不能为空";
            return;
        }

        try
        {
            if (IsEditMode)
            {
                var identity = IdentityDb.GetById(_identityId);
                if (identity != null)
                {
                    identity.Name = Name.Trim();
                    identity.Enable = Enable;
                    identity.EnableUpdate = EnableUpdate;
                    identity.Birthday = string.IsNullOrWhiteSpace(Birthday) ? null : Birthday.Trim();
                    IdentityDb.Update(identity);
                }
            }
            else
            {
                var newIdentity = new IdentityInfo
                {
                    Name = Name.Trim(),
                    Enable = Enable,
                    EnableUpdate = EnableUpdate,
                    Birthday = string.IsNullOrWhiteSpace(Birthday) ? null : Birthday.Trim(),
                };
                IdentityDb.Add(newIdentity);
            }

            Saved?.Invoke(_identityId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败: {ex.Message}";
        }
    }
}
