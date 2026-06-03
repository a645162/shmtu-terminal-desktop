# 应用结构

`shmtu-terminal-desktop` 采用经典的 MVVM 三层架构，所有业务规则位于 `shmtu-dotnet-lib`，UI 仅承担渲染与用户输入。

## 三层架构

```
┌─────────────────────────────────────────────────────┐
│  Views (XAML)                                       │
│  - MainWindow.axaml, AccountEditWindow.axaml 等     │
│  - 只做 DataBinding 和事件转发                       │
└────────────────┬────────────────────────────────────┘
                 │ DataContext + ICommand
┌────────────────▼────────────────────────────────────┐
│  ViewModels (ReactiveUI)                            │
│  - MainWindowViewModel, UserViewModel 等            │
│  - 持有 ObservableCollection<T> 状态                 │
│  - 通过 ServiceLocator 调用 Service                 │
└────────────────┬────────────────────────────────────┘
                 │ DI / Service Locator
┌────────────────▼────────────────────────────────────┐
│  Services (TomlConfigService, BillSyncService 等)   │
│  - 纯业务逻辑（不引用 Avalonia）                      │
│  - 通过接口暴露能力，便于测试和替换                    │
└────────────────┬────────────────────────────────────┘
                 │ 项目引用
┌────────────────▼────────────────────────────────────┐
│  shmtu-dotnet-lib (第三方类库)                       │
│  - CAS 认证、BillSync、HTML 解析、分类器、导出         │
└─────────────────────────────────────────────────────┘
```

## 源码目录

```
shmtu-terminal-desktop/
├── App.axaml(.cs)              应用入口 + DI 容器
├── Program.cs                  启动参数解析
├── ViewLocator.cs              ViewModel → View 映射
│
├── Views/                      XAML 视图
│   ├── MainWindow.axaml        主窗口（含 Tab 容器）
│   ├── Startup/                启动密码、身份选择
│   ├── User/                   身份管理、账号编辑
│   └── Component/              复用控件（账单卡片等）
│
├── ViewModels/                 ReactiveUI ViewModel
│   ├── MainWindowViewModel.cs  主 ViewModel，持有 4 个子 Tab VM
│   ├── ViewModelBase.cs        基类（实现 INotifyPropertyChanged）
│   ├── Startup/                启动流程 VM
│   ├── User/                   身份/账号管理 VM
│   └── MainWindowTab/          首页/账单/统计/设置 Tab VM
│
├── Services/                   业务服务（纯 C#，无 UI 依赖）
│   ├── ServiceLocator.cs       简易服务定位器
│   ├── LoggingService.cs       Serilog 封装
│   ├── Sync/                   同步服务
│   ├── Statistics/             统计服务
│   ├── Export/                 导出/导入/快照
│   ├── Config/                 TOML 配置
│   ├── Security/               加密服务
│   ├── Session/                会话刷新
│   ├── Navigation/             导航
│   └── BillClassification/     账单分类
│
├── Models/                     实体与 DTO
│   ├── User/                   Identity, Account DTO
│   ├── Bill/                   BillItem DTO
│   └── ...
│
├── Database/                   SqlSugar 封装与迁移
│
├── Controls/                   自定义控件
│
├── Assets/                     图标、字体、主题
│
└── Styles/                     全局样式

shmtu-dotnet-lib/Core/shmtu-dotnet-lib/    业务类库
├── cas/                                CAS 认证
│   ├── auth/                           登录流程
│   └── captcha/                        验证码解析
├── sync/                               BillSync 同步抽象
├── parser/                             HTML/JSON 解析
├── datatype/                           数据类型
├── export/                             导出
├── Classifier/                         账单分类
└── utils/                              工具类
```

## MVVM 数据流

```csharp
// View 触发 Command
this.BindCommand(ViewModel, vm => vm.SyncCommand, v => v.SyncButton);

// ViewModel 收到命令 → 调 Service
public ReactiveCommand<Unit, Unit> SyncCommand { get; } = ReactiveCommand.Create(async () =>
{
    var result = await _billSyncService.SyncAsync(SelectedAccount, ...);
    Bills.AddRange(result.NewBills);
});

// Service 写入数据库并通过事件/回调通知进度
await _billSyncService.SyncAsync(account, progress =>
{
    // 跨线程 → 通过 MainThread 回到 UI 线程
    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        SyncProgress = progress;
    });
});
```

## View ↔ ViewModel 定位

`ViewLocator.cs` 通过命名约定自动匹配：

- `MainWindowViewModel` → `MainWindow.axaml`
- `AccountEditViewModel` → `AccountEditWindow.axaml`

> 自定义 ViewModel 必须放在 `ViewModels/` 目录下且文件以 `ViewModel` 结尾；自定义 View 必须放在 `Views/` 下。

## 下一步

阅读 [数据流与状态](/advanced/data-flow) 理解跨层通信。
