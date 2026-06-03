# 数据流与状态

应用使用 **ReactiveUI + ServiceLocator** 实现响应式状态管理。本节解释数据如何从 Service 流向 View，以及如何反向回传。

## 状态分层

应用的状态按"作用域"分三层：

| 层级 | 生命周期 | 存储 | 例子 |
|---|---|---|---|
| **应用级** | 应用启动到关闭 | 内存单例 | 当前主题、当前身份、当前选中 Tab |
| **会话级** | 身份切换 | 内存 + 加密文件 | 账号列表、最后一次同步状态 |
| **持久级** | 跨会话 | 加密文件 + Sqlite | 配置、账单、快照 |

## ServiceLocator

`Services/ServiceLocator.cs` 是一个静态单例容器，所有 Service 在 `App.axaml.cs` 启动时注册：

```csharp
// App.axaml.cs (简化)
public override void OnFrameworkInitializationCompleted()
{
    var services = new ServiceCollection();

    services.AddSingleton<ITomlConfigService, TomlConfigService>();
    services.AddSingleton<IEncryptionService, EncryptionService>();
    services.AddSingleton<IBillSyncService, BillSyncService>();
    services.AddSingleton<IStatisticsService, StatisticsService>();
    services.AddSingleton<IBillExportService, BillExportService>();
    services.AddSingleton<IDataSnapshotService, DataSnapshotService>();
    services.AddSingleton<ILoggingService, LoggingService>();
    services.AddSingleton<ISessionRefreshService, SessionRefreshService>();
    services.AddSingleton<INavigationService, NavigationService>();
    services.AddSingleton<IBillClassifier, BillClassifier>();

    // ...
    ServiceLocator.Initialize(services.BuildServiceProvider());
}
```

ViewModel 通过 `ServiceLocator.Get<T>()` 拿到 Service：

```csharp
public class MainWindowViewModel : ViewModelBase
{
    private readonly IBillSyncService _sync = ServiceLocator.Get<IBillSyncService>();
    private readonly IStatisticsService _stats = ServiceLocator.Get<IStatisticsService>();
}
```

> ServiceLocator 简化了 ViewModel 之间的依赖，但牺牲了部分可测试性。新代码建议优先用构造函数注入。

## ReactiveUI 状态绑定

`ViewModelBase` 实现 `INotifyPropertyChanged` + `IReactiveObject`，派生类用 `RaiseAndSetIfChanged` 触发更新：

```csharp
private string _selectedAccountId = "";
public string SelectedAccountId
{
    get => _selectedAccountId;
    set => this.RaiseAndSetIfChanged(ref _selectedAccountId, value);
}
```

View 端用 `this.Bind(...)` / `this.OneWayBind(...)` 绑定：

```xml
<ComboBox ItemsSource="{Binding Accounts}"
          SelectedItem="{Binding SelectedAccount, Mode=TwoWay}"/>
```

## ObservableCollection 与增量更新

账单列表是 `ObservableCollection<BillItem>`，新同步到的条目通过 `AddRange` 一次性插入，UI 自动刷新：

```csharp
public ObservableCollection<BillItem> Bills { get; } = new();

public async Task OnSyncComplete(SyncResult result)
{
    Bills.AddRange(result.NewBills);
    await _stats.InvalidateCacheAsync();
}
```

> 对**大数据量**（>10000 条）的列表，避免一次性 AddRange，应分页 + 增量加载，否则首次渲染会卡顿。

## 跨线程通信

网络/IO 任务在后台线程，UI 更新必须在 UI 线程：

```csharp
public async Task RunSyncAsync()
{
    await Task.Run(async () =>
    {
        var progress = new Progress<SyncProgress>(p =>
        {
            // Progress<T> 自动捕获 SynchronizationContext
            SyncProgress = p;
        });

        await _billSyncService.SyncAsync(SelectedAccount, progress);
    });
}
```

或者用 Avalonia 的 `Dispatcher` 显式调度：

```csharp
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
    SyncProgress = newProgress;
});
```

## 事件总线

跨 ViewModel 通信（如"身份切换通知所有 Tab"）用简单的 `MessageBus`：

```csharp
// 发布
MessageBus.Current.SendMessage(new IdentityChangedMessage(newIdentity));

// 订阅
MessageBus.Current.Listen<IdentityChangedMessage>()
    .Subscribe(msg => Reload());
```

## 状态持久化时机

| 触发点 | 持久化什么 |
|---|---|
| 用户点击"保存"按钮 | 配置变更 |
| 同步完成 | 账单写入 Sqlite |
| 应用退出 | 配置自动保存 |
| 用户点击"创建快照" | bills.db 完整副本 |
| 启动密码变更 | identities.enc 重新加密并写盘 |

## 下一步

阅读 [同步与验证码](/advanced/sync-and-captcha) 了解 Service 层的实现细节。
