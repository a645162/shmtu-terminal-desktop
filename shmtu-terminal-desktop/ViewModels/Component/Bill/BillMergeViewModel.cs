using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.ViewModels.MainWindowTab;
using shmtu.terminal.desktop.Models.Bill;

namespace shmtu.terminal.desktop.ViewModels.Component.Bill;

public class BillMergeViewModel : ViewModelBase
{
    private readonly int _identityId;

    public ObservableCollection<BillDisplayItem> SelectableBills { get; } = [];

    private BillDisplayItem? _primaryBill;
    public BillDisplayItem? PrimaryBill
    {
        get => _primaryBill;
        set => this.RaiseAndSetIfChanged(ref _primaryBill, value);
    }

    private string? _mergeNote;
    public string? MergeNote
    {
        get => _mergeNote;
        set => this.RaiseAndSetIfChanged(ref _mergeNote, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public event Action? Saved;
    public event Action? Cancelled;

    public BillMergeViewModel(int identityId, BillDisplayItem primary)
    {
        _identityId = identityId;
        _primaryBill = primary;

        // Load recent bills for selection
        InitDb.InitIdentityDb(identityId);
        var bills = BillMergedDb.GetAll(identityId, 1, 200)
            .Where(b => b.Id != primary.Id && !b.IsCombined)
            .Take(50)
            .Select(b => BillDisplayItem.FromBillMerged(b))
            .ToList();
        foreach (var b in bills) SelectableBills.Add(b);

        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(() => Cancelled?.Invoke());
    }

    private void Save()
    {
        if (PrimaryBill == null) return;

        var selectedIds = SelectableBills
            .Where(b => b.IsSelectedForMerge)
            .Select(b => b.Id)
            .ToList();

        if (selectedIds.Count == 0)
        {
            ErrorMessage = "请至少选择一条账单进行合并";
            return;
        }

        try
        {
            InitDb.InitIdentityDb(_identityId);
            BillMergedDb.MergeBills(_identityId, PrimaryBill.Id, selectedIds, MergeNote);
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"合并失败: {ex.Message}";
        }
    }
}
