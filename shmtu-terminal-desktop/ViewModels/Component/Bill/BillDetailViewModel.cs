using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.ViewModels.MainWindowTab;

namespace shmtu.terminal.desktop.ViewModels.Component.Bill;

public class BillDetailViewModel : ViewModelBase
{
    public int Id => _item.Id;
    public string DateTimeFormatted => _item.DateTimeFormatted ?? "";
    public string ItemType => _item.ItemType ?? "";
    public string? Number => _item.Number;
    public string? TargetUser => _item.TargetUser;
    public string? MoneyStr => _item.MoneyStr;
    public double? Money => _item.Money;
    public string MoneyColor => _item.MoneyColor;
    public string? Method => _item.Method;
    public string? StatusStr => _item.StatusStr;
    public string StatusBackground => _item.StatusBackground;
    public string StatusForeground => _item.StatusForeground;
    public string? ClassificationType => _item.ClassificationType;
    public bool IsCombined => _item.IsCombined;
    public string? NumberList => _item.NumberList;
    public string? SourceAccountId => _item.SourceAccountId;
    public bool IsManual => _item.IsManual;
    public string Position => _item.Position ?? "";

    private string? _notes;
    public string? Notes
    {
        get => _notes;
        set => this.RaiseAndSetIfChanged(ref _notes, value);
    }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveNotesCommand { get; }
    public ReactiveCommand<string?, Unit> CopyFieldCommand { get; }

    private readonly BillDisplayItem _item;
    private readonly int _identityId;

    public event Action? Closed;
    public event Action? NotesSaved;

    public BillDetailViewModel(BillDisplayItem item)
    {
        _item = item;
        _identityId = 0; // Will not persist notes without identityId
        _notes = item.Notes;

        CloseCommand = ReactiveCommand.Create(() => Closed?.Invoke());
        SaveNotesCommand = ReactiveCommand.Create(SaveNotes);
        CopyFieldCommand = ReactiveCommand.Create<string?>(CopyField);
    }

    public BillDetailViewModel(BillDisplayItem item, int identityId) : this(item)
    {
        _identityId = identityId;
    }

    private void SaveNotes()
    {
        if (_identityId <= 0) return;

        try
        {
            InitDb.InitIdentityDb(_identityId);
            BillMergedDb.UpdateNotes(_identityId, _item.Id, Notes ?? "");
            NotesSaved?.Invoke();
        }
        catch
        {
            // Silently fail — user can retry
        }
    }

    private void CopyField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            var desktop = Avalonia.Application.Current?.ApplicationLifetime
                as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            if (desktop?.MainWindow == null) return;
            var tl = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
            if (tl?.Clipboard != null)
            {
                var setText = tl.Clipboard.GetType().GetMethod("SetTextAsync");
                setText?.Invoke(tl.Clipboard, [value]);
            }
        }
        catch
        {
            // Clipboard access can fail in some environments
        }
    }
}
