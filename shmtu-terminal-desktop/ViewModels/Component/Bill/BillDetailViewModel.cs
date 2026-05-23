using System.Reactive;
using ReactiveUI;
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
    public string? Method => _item.Method;
    public string? StatusStr => _item.StatusStr;
    public string? ClassificationType => _item.ClassificationType;
    public bool IsCombined => _item.IsCombined;
    public string? NumberList => _item.NumberList;
    public string? SourceAccountId => _item.SourceAccountId;
    public bool IsManual => _item.IsManual;

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    private readonly BillDisplayItem _item;

    public event Action? Closed;

    public BillDetailViewModel(BillDisplayItem item)
    {
        _item = item;
        CloseCommand = ReactiveCommand.Create(() => Closed?.Invoke());
    }
}
