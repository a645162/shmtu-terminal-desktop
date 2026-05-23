using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using shmtu.terminal.desktop.Database;
using shmtu.terminal.desktop.Database.Manage.Bill;
using shmtu.terminal.desktop.Models.Bill;
using shmtu.terminal.desktop.Services.BillClassification;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Navigation;
using shmtu.terminal.desktop.ViewModels.Component.Bill;

namespace shmtu.terminal.desktop.ViewModels.MainWindowTab;

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
    public string? ClassificationType { get; set; }

    public static BillDisplayItem FromBillMerged(BillMerged bill, BillClassifier? classifier = null)
    {
        var money = bill.Money ?? 0;
        var clsResult = classifier?.Classify(bill);
        return new BillDisplayItem
        {
            Id = bill.Id,
            DateTimeFormatted = bill.DateTimeFormatted,
            ItemType = clsResult != null
                ? $"{bill.ItemType} ({clsResult.Type})"
                : bill.ItemType,
            Number = bill.Number,
            TargetUser = bill.TargetUser,
            MoneyStr = bill.MoneyStr,
            Money = bill.Money,
            MoneyColor = money < 0 ? "#F44336" : "#4CAF50",
            Method = bill.Method,
            StatusStr = bill.StatusStr,
            IsCombined = bill.IsCombined,
            NumberList = bill.NumberList,
            SourceAccountId = bill.SourceAccountId,
            IsManual = bill.IsManual,
            ClassificationType = clsResult?.Type,
        };
    }
}

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

    public ReactiveCommand<Unit, Unit> FirstPageCommand { get; }
    public ReactiveCommand<Unit, Unit> PrevPageCommand { get; }
    public ReactiveCommand<Unit, Unit> NextPageCommand { get; }
    public ReactiveCommand<Unit, Unit> LastPageCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyNumberCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyMoneyCommand { get; }
    public ReactiveCommand<Unit, Unit> ViewDetailCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteBillCommand { get; }
    public ReactiveCommand<Unit, Unit> MergeBillCommand { get; }

    public BillTabViewModel()
    {
        FilterViewModel = new BillFilterViewModel();
        _classifier = new BillClassifier();

        FilterViewModel.FilterChanged += () => { _currentPage = 1; RefreshData(); };
        FilterViewModel.SyncRequested += OnSyncRequested;

        FirstPageCommand = ReactiveCommand.Create(() => GoToPage(1));
        PrevPageCommand = ReactiveCommand.Create(() => GoToPage(Math.Max(1, _currentPage - 1)));
        NextPageCommand = ReactiveCommand.Create(() =>
        {
            var totalPages = GetTotalPages();
            GoToPage(Math.Min(totalPages, _currentPage + 1));
        });
        LastPageCommand = ReactiveCommand.Create(() => GoToPage(GetTotalPages()));

        CopyNumberCommand = ReactiveCommand.CreateFromTask(CopyNumberAsync);
        CopyMoneyCommand = ReactiveCommand.CreateFromTask(CopyMoneyAsync);
        ViewDetailCommand = ReactiveCommand.Create(ViewDetail);
        DeleteBillCommand = ReactiveCommand.CreateFromTask(DeleteBillAsync);
        MergeBillCommand = ReactiveCommand.Create(MergeBill);
    }

    public event Action? SyncRequested;

    private void OnSyncRequested() => SyncRequested?.Invoke();

    public void RefreshData()
    {
        if (_identityId <= 0) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            InitDb.InitIdentityDb(_identityId);

            var (startTs, endTs) = FilterViewModel.GetTimeRange();
            var searchText = FilterViewModel.SearchText?.Trim() ?? "";
            var billType = FilterViewModel.SelectedBillType;

            // CRITICAL 4: 过滤下推到数据库层
            var (bills, count) = BillMergedDb.GetFiltered(
                _identityId,
                _currentPage,
                PageSize,
                startTs,
                endTs,
                searchText,
                billType == "全部" ? null : billType);

            TotalCount = count;

            // 加载所有过滤后的项用于分类（数量有限，最多 PageSize）
            var displayItems = bills
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

    /// <summary>
    /// CRITICAL 5: 剪贴板复制 — 使用 Avalonia TopLevel API
    /// </summary>
    private async Task CopyNumberAsync()
    {
        if (SelectedBillItem?.Number == null) return;
        await CopyToClipboardAsync(SelectedBillItem.Number);
    }

    /// <summary>
    /// CRITICAL 5: 剪贴板复制金额
    /// </summary>
    private async Task CopyMoneyAsync()
    {
        if (SelectedBillItem?.MoneyStr == null) return;
        await CopyToClipboardAsync(SelectedBillItem.MoneyStr);
    }

    private static async Task CopyToClipboardAsync(string text)
    {
        try
        {
            var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
            var tl = desktop?.MainWindow;
            if (tl != null)
            {
                var topLvl = Avalonia.Controls.TopLevel.GetTopLevel(tl);
                if (topLvl?.Clipboard is { } cb)
                {
                    var setText = cb.GetType().GetMethod("SetTextAsync");
                    if (setText != null)
                        await (Task)setText.Invoke(cb, [text])!;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard error: {ex.Message}");
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return TopLevel.GetTopLevel(desktop.MainWindow);
        return null;
    }

    /// <summary>
    /// CRITICAL 6: ViewDetailCommand — 打开账单详情对话框
    /// </summary>
    private void ViewDetail()
    {
        if (SelectedBillItem == null) return;

        var vm = new BillDetailViewModel(SelectedBillItem);
        NavigationService.Instance.OpenWindow("BillDetail", vm);
    }

    /// <summary>
    /// CRITICAL 7: MergeBillCommand — 打开账单合并对话框
    /// </summary>
    private void MergeBill()
    {
        if (SelectedBillItem == null || _identityId <= 0) return;
        var vm = new BillMergeViewModel(_identityId, SelectedBillItem);
        vm.Saved += () => RefreshData();
        NavigationService.Instance.OpenWindow("BillMerge", vm);
    }

    /// <summary>
    /// CRITICAL 9: 删除前显示确认对话框
    /// </summary>
    private async Task DeleteBillAsync()
    {
        if (SelectedBillItem == null || _identityId <= 0) return;

        var mainWindow = GetTopLevel();
        if (mainWindow == null) return;

        var confirmWindow = new Window
        {
            Title = "确认删除",
            Width = 320, Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            CanResize = false,
        };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = "确认删除这条账单记录吗？", FontSize = 14 });
        panel.Children.Add(new TextBlock
        {
            Text = $"{SelectedBillItem?.DateTimeFormatted} {SelectedBillItem?.ItemType}",
            FontSize = 12,
            Foreground = Avalonia.Media.Brushes.Gray
        });
        var btnPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        var deleteBtn = new Button { Content = "删除", Width = 80 };
        deleteBtn.Click += (_, _) => confirmWindow.Close(true);
        var cancelBtn = new Button { Content = "取消", Width = 80 };
        cancelBtn.Click += (_, _) => confirmWindow.Close(false);
        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(deleteBtn);
        panel.Children.Add(btnPanel);
        confirmWindow.Content = panel;

        var result = await confirmWindow.ShowDialog<bool>((Window)(mainWindow ?? GetTopLevel()!));

        if (!result) return;

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
}
