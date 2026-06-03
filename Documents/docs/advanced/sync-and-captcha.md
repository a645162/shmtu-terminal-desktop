# 同步与验证码

本节讲解 `BillSyncService` 的内部链路、`IBillStore` 抽象、验证码解析器接口与 OCR 推理。

## 同步链路总览

```
BillSyncService.SyncAsync(account, options, progress, ct)
   │
   ├─ 1. 获取 Captcha (ICaptchaResolver.ResolveAsync)
   │      ├─ ManualCaptchaResolver → 弹窗让用户输入
   │      └─ RemoteOcrCaptchaResolver → 调本地 OCR HTTP
   │
   ├─ 2. 登录 CAS (EpayAuth.LoginAsync)  → Cookie
   │
   ├─ 3. 循环翻页
   │      ├─ 请求第 N 页 HTML (Flurl.Http)
   │      ├─ HtmlAgilityPack 解析
   │      ├─ 去重（按交易号）
   │      └─ 早停（连续 N 条已存在）
   │
   ├─ 4. 写入本地存储（通过 IBillStore）
   │
   └─ 5. 推送进度（IProgress<SyncProgress>）
```

## IBillStore 抽象

`shmtu-dotnet-lib/sync/BillSync.cs` 定义了存储抽象，桌面应用通过 `BillDatabaseStore`（SqlSugar 实现）来适配：

```csharp
public interface IBillStore
{
    bool Contains(string number);
    void Merge(List<BillItemInfo> newBills);
}
```

设计原则：**库不关心是 Sqlite / JSON / 内存**。这使得 BillSync 可以独立测试：

```csharp
// 单元测试中
var store = new InMemoryBillStore();
var result = await BillSync.RunAsync(account, store, options);
Assert.Equal(2, result.NewCount);
```

## 验证码解析器

`cas/captcha/` 目录下定义了 `ICaptchaResolver` 接口：

```csharp
public interface ICaptchaResolver
{
    Task<CaptchaAnswer> ResolveAsync(byte[] imageBytes, CancellationToken ct = default);
}
```

`CaptchaAnswerKind` 区分**有答案**（用户输入了）和**自动**（OCR 识别）：

```csharp
public enum CaptchaAnswerKind { Manual, Auto }

public record CaptchaAnswer(string Text, CaptchaAnswerKind Kind);
```

### 三种实现

1. **ManualCaptchaResolver**：直接抛 `CaptchaRequiresManualInputException`，由 UI 捕获后弹窗。
2. **RemoteOcrCaptchaResolver**：HTTP POST 图片到 `http://127.0.0.1:5000/captcha/recognize`。
3. **RemoteOcrHttpCaptchaResolver**：基于 Flurl 的链式调用版本，含重试与超时。

> UI 层的"弹窗让用户输入"不放在 `shmtu-dotnet-lib` 中，而是 ViewModel 监听 `CaptchaRequiresManualInputException` 后触发。

## 早停机制

增量同步的关键优化。`BillSyncOptions.EarlyStopThreshold`（默认 5）控制：

```
翻到第 3 页：
  - 条目 1: 已存在 (count=1)
  - 条目 2: 已存在 (count=2)
  - 条目 3: 新增   (count=0)
  - 条目 4: 新增   (count=0)
  - 条目 5: 已存在 (count=1)
翻到第 4 页：
  - 条目 1: 已存在 (count=2)
  - 条目 2: 已存在 (count=3)
  - 条目 3: 已存在 (count=4)
  - 条目 4: 已存在 (count=5)  ← 触发早停
  - 条目 5: 已存在 (count=6)  ← 早停后不再翻页
```

这避免了每次增量同步都翻 100+ 页。

## ONNX OCR 推理

`shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-lib/` 包含三个模型：

| 模型 | 文件 | 用途 | 大小 |
|---|---|---|---|
| 等号识别 | `resnet18_equal_symbol_latest.onnx` | 区分 `=` `≈` `≠` | 42.6MB |
| 运算符识别 | `resnet18_operator_latest.onnx` | `+` `-` `×` `÷` | 42.6MB |
| 数字识别 | `resnet34_digit_latest.onnx` | 0-9 数字 | 81.2MB |

推理流程（`CasOcr.cs`）：

```
输入验证码图片 (PNG/JPG)
   ↓
CasCaptchaImage.Process
   ├─ 灰度化
   ├─ 二值化
   ├─ 切割字符 (垂直投影)
   └─ 归一化到 32x32
   ↓
对每个字符调用 ResNet ONNX
   ↓
拼接结果字符串
```

**典型识别耗时**：单张验证码 < 50ms（CPU），< 10ms（GPU 需 CUDA EP）。

## HTTP OCR 服务

`shmtu-ocr-onnx-server/` 暴露以下端点（默认 5000 端口）：

| 方法 | 路径 | 入参 | 出参 |
|---|---|---|---|
| GET | `/health` | - | `{status: "ok"}` |
| POST | `/captcha/recognize` | `{image: base64}` | `{text: "1234"}` |
| POST | `/captcha/recognize-file` | multipart file | `{text: "1234"}` |
| GET | `/version` | - | `{version, modelDate}` |

Docker 部署：

```bash
docker-compose -f shmtu-dotnet-lib/ocr/docker-compose.yml up -d
```

## 进度推送

`BillSyncService` 在每页拉取后调用 `IProgress<SyncProgress>.Report`：

```csharp
public record SyncProgress(
    int CurrentPage,
    int TotalPages,        // 估算（基于服务器返回的总页数）
    int NewCount,
    int PagesFetched
);
```

UI 用 `Progress<T>` 自动捕获 SynchronizationContext，无需手动 `Dispatcher.Invoke`。

## 下一步

阅读 [配置与存储](/advanced/config-and-storage) 了解配置和数据库细节。
