# 高级文档总览

本节面向**开发者与维护者**，解释应用内部的架构、状态管理、同步机制与发布流程。

## 阅读建议

按以下顺序阅读，能在最短时间内建立完整心智模型：

1. [应用结构](/advanced/app-structure) — 三层架构（Avalonia 视图 / ViewModel / Services）
2. [数据流与状态](/advanced/data-flow) — ReactiveUI 状态、ServiceLocator、跨层通信
3. [同步与验证码](/advanced/sync-and-captcha) — BillSyncService 链路、验证码解析器
4. [配置与存储](/advanced/config-and-storage) — TomlConfigService、SqlSugar、加密、快照
5. [构建与发布](/advanced/build-and-release) — dev / docs / 发布 / CI

## 与 Tauri 版的对应关系

本仓库与 [shmtu-terminal-tauri](https://github.com/a645162/shmtu-terminal-tauri) 是同一个产品的两个独立实现，共享业务模型：

| 概念 | Tauri 版 | .NET 版 |
|---|---|---|
| 后端语言 | Rust | C# 12 |
| UI 框架 | React + Fluent UI | Avalonia 12 + XAML |
| 状态管理 | Zustand | ReactiveUI |
| 数据库 | Sqlite (rusqlite) | Sqlite (SqlSugarCore) |
| 配置文件 | TOML | TOML (Tomlyn) |
| 日志 | tracing | Serilog |
| 图表 | Recharts | LiveCharts2 |
| CAS 库 | shmtu-cas-rs | shmtu-dotnet-lib |
| OCR 库 | shmtu-cas-ocr-server (C++) / shmtu-cas-onnx (Rust) | shmtu-ocr-onnx-lib |

## 关键设计原则

- **类库与 UI 解耦**：`shmtu-dotnet-lib` 是纯类库，桌面应用只做 UI 适配。`shmtu-terminal-desktop` 不应包含业务规则。
- **可注入的同步抽象**：`BillSync` 接受 `IBillStore` 接口，调用方决定存储后端（SqlSugar / 内存 / 其他）。
- **加密与业务分离**：`EncryptionService` 只负责加解密，不关心是哪个字段。
- **MVVM 严格分层**：View 不直接调 Service，通过 ViewModel 转发；ViewModel 通过 `ServiceLocator` 拿到 Service。

## 模块依赖图

```
shmtu-terminal-desktop (Avalonia UI)
    ├─→ shmtu-dotnet-lib (CAS / BillSync / Parser / Classifier)
    │      └─→ Flurl.Http + HtmlAgilityPack
    └─→ shmtu-ocr-onnx-lib (ONNX 推理)
           └─→ Microsoft.ML.OnnxRuntime
```

## 下一步

阅读 [应用结构](/advanced/app-structure) 了解三层架构。
