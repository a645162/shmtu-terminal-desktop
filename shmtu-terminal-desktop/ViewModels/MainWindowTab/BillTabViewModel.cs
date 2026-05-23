using System.Collections.ObjectModel;
using System.Reactive;
using System.Text;
using ReactiveUI;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Services.BillClassification;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.ViewModels.Component.Bill;

namespace shmtu.terminal.desktop.ViewModels.MainWindowTab;

/// <summary>
/// Display wrapper for BillMerged items in the DataGrid
/// Adds computed properties like MoneyColor
/// </summary>
public class BillDisplayItem : ViewModelBase
{
    public int Id { get; set; }
    public string? DateTimeFormatted { get; set; }
    public string? ItemType { get; set; }
    public string? Number { get; set; }
    public string? TargetUser { get; set; }
    public string? MoneyStr { get; set; }
    public double? Money { get; set; }
    public string MoneyColor { get; set; } = "#F44336";
    public string? Method { get; set; }
    public string? StatusStr { get; set; }
    public bool IsCombined { get; set; }
    public string? NumberList { get; set; }
    public string? SourceAccountId { get; set; }
    public bool IsManual { get; set; }

    public static BillDisplayItem FromBillMerged(BillMerged bill, BillClassifier? classifier = null)
    {
        var money = bill.Money ?? 0;
        return new BillDisplayItem
        {
            Id = bill.Id,
            DateTimeFormatted = bill.DateTimeFormatted,
            ItemType = classifier != null
                ? $"{bill.ItemType} ({classifier.Classify(bill).Type})"
                : bill.ItemType,
            Number = bill.Number,
            TargetUser = bill.TargetUser,
            MoneyStr = bill.MoneyStr,
            Money = bill.Money,
            MoneyColor = money < 0 ? "#F44336" : "#4CAF50", // Red for expense, green for income
            Method = bill.Method,
            StatusStr = bill.StatusStr,
            IsCombined = bill.IsCombined,
            NumberList = bill.NumberList,
            SourceAccountId = bill.SourceAccountId,
            IsManual = bill.IsManual,
        };
    }
}

/// <summary>
/// ViewModel for the Bill tab
/// XAML bindings: FilterViewModel, PaginationInfo, FirstPageCommand, PrevPageCommand,
///   NextPageCommand, LastPageCommand, BillItems, SelectedBillItem,
///   CopyNumberCommand, CopyMoneyCommand, ViewDetailCommand, DeleteBillCommand, MergeBillCommand
/// </summary>
public class BillTabViewModel : ViewModelBase
{
    private const int PageSize = 50;

    private int _identityId;
    public int IdentityId
    {
        get => _identityId;
        set
        {
            this.RaiseAndSetIfChanged(ref _identityId, value);
            _currentPage = 1;
            RefreshData();
        }
    }

    private int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set => this.RaiseAndSetIfChanged(ref _totalCount, value);
    }

    private string _paginationInfo = "";
    public string PaginationInfo
    {
        get => _paginationInfo;
        set => this.RaiseAndSetIfChanged(ref _paginationInfo, value);
    }

    public ObservableCollection<BillDisplayItem> BillItems { get; } = [];

    private BillDisplayItem? _selectedBillItem;
    public BillDisplayItem? SelectedBillItem
    {
        get => _selectedBillItem;
        set => this.RaiseAndSetIfChanged(ref _selectedBillItem, value);
    }

    public BillFilterViewModel FilterViewModel { get; }
    private readonly BillClassifier _classifier;

    // Pagination commands
    public ReactiveCommand<Unit, Unit> FirstPageCommand { get; }
    public ReactiveCommand<Unit, Unit> PrevPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LastPageCommand { get; }

    // Context menu commands
    public ReactiveCommand<Unit, Unit> CopyNumberCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyMoneyCommand { get; }
    public ReactiveCommand<Unit, Unit> ViewDetailCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteBillCommand { get; }
    public ReactiveCommand<Unit, Unit> MergeBillCommand { get; }

    public BillTabViewModel()
    {
        FilterViewModel = new BillFilterViewModel();
        _classifier = new BillClassifier();

        // Subscribe to filter changes
        FilterViewModel.FilterChanged += () => RefreshData();
        FilterViewModel.SyncRequested += OnSyncRequested;

        // Pagination
        FirstPageCommand = ReactiveCommand.Create(() => GoToPage(1));
        PrevPageCommand = ReactiveCommand.Create(() => GoToPage(Math.Max(1, _currentPage - 1)));
        NextPageCommand = ReactiveCommand.Create(() =>
        {
            var totalPages = GetTotalPages();
            GoToPage(Math.Min(totalPages, _currentPage + 1));
        });
        LastPageCommand = ReactiveCommand.Create(() => GoToPage(GetTotalPages()));

        // Context menu
        CopyNumberCommand = ReactiveCommand.Create(CopyNumber);
        CopyMoneyCommand = ReactiveCommand.Create(CopyMoney);
        ViewDetailCommand = ReactiveCommand.Create(ViewDetail);
        DeleteBillCommand = ReactiveCommand.Create(DeleteBill);
        MergeBillCommand = ReactiveCommand.Create(MergeBill);
    }

    /// <summary>
    /// Event raised when sync is requested
    /// </summary>
    public event Action? SyncRequested;

    private void OnSyncRequested()
    {
        SyncRequested?.Invoke();
    }

    public void RefreshData()
    {
        if (_identityId <= 0) return;

        IsLoading = true;

        try
        {
            InitDb.InitIdentityDb(_identityId);

            TotalCount = BillMergedDb.GetCount(_identityId);
            var bills = BillMergedDb.GetAll(_identityId, _currentPage, PageSize);

            // Apply filters
            var (startTs, endTs) = FilterViewModel.GetTimeRange();
            var filteredBills = bills.AsEnumerable();

            if (startTs.HasValue)
                filteredBills = filteredBills.Where(b => b.Timestamp >= startTs.Value);
            if (endTs.HasValue)
                filteredBills = filteredBills.Where(b => b.Timestamp <= endTs.Value);

            if (!string.IsNullOrEmpty(FilterViewModel.SearchText))
            {
                var search = FilterViewModel.SearchText.ToLowerInvariant();
                filteredBills = filteredBills.Where(b =>
                    (b.ItemType?.ToLowerInvariant().Contains(search) == true) ||
                    (b.TargetUser?.ToLowerInvariant().Contains(search) == true) ||
                    (b.Number?.ToLowerInvariant().Contains(search) == true));
            }

            if (FilterViewModel.SelectedBillType != "全部")
            {
                var selectedType = FilterViewModel.SelectedBillType;
                filteredBills = filteredBills.Where(b =>
                {
                    var result = _classifier.Classify(b);
                    return result.Type == selectedType;
                });
            }

            var displayItems = filteredBills
                .Select(b => BillDisplayItem.FromBillMerged(b, _classifier))
                .ToList();

            BillItems.Clear();
            foreach (var item in displayItems)
            {
                BillItems.Add(item);
            }

            UpdatePaginationInfo();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载账单数据失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GoToPage(int page)
    {
        CurrentPage = page;
        RefreshData();
    }

    private int GetTotalPages()
    {
        return TotalCount <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    private void UpdatePaginationInfo()
    {
        var totalPages = GetTotalPages();
        PaginationInfo = $"第 {_currentPage}/{totalPages} 页，共 {TotalCount} 条记录";
    }

    private void CopyNumber()
    {
        if (SelectedBillItem?.Number == null) return;
        try
        {
            var clipboard = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard
                : null;
            // For Avalonia 11+, use TopLevel clipboard
            // Simple approach: just set the text
            System.Diagnostics.Debug.WriteLine($"Copy number: {SelectedBillItem.Number}");
        }
        catch { }
    }

    private void CopyMoney()
    {
        if (SelectedBillItem?.MoneyStr == null) return;
        try
        {
            System.Diagnostics.Debug.WriteLine($"Copy money: {SelectedBillItem.MoneyStr}");
        }
        catch { }
    }

    private void ViewDetail()
    {
        if (SelectedBillItem == null) return;
        // TODO: Open detail dialog/window
    }

    private void DeleteBill()
    {
        if (SelectedBillItem == null || _identityId <= 0) return;

        try
        {
            BillMergedDb.DeleteManual(_identityId, SelectedBillItem.Id);
            RefreshData();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"删除记录失败: {ex.Message}";
        }
    }

    private void MergeBill()
    {
        // TODO: Open merge dialog — select multiple items and merge
        // For now, mark the current item as combined
    }
}
