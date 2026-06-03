# FAQ

## 通用问题

### 这个项目是做什么的？

SHMTU Terminal Desktop 是上海海事大学校园终端桌面版应用，基于 .NET 8 和 Avalonia 构建。主要功能包括 CAS 统一认证登录、校园卡账单同步与分类、验证码自动识别（OCR）等。原为"数字海大工具"，现使用 .NET 8 + Avalonia 完全重写。

### 支持哪些操作系统？

应用基于 Avalonia UI 框架构建，支持以下平台：

- Windows 10 及以上版本
- Linux (x64 / ARM64)
- macOS 12 及以上版本

### 需要安装什么才能运行？

需要安装 .NET 8.0 运行时。如果要开发或从源码构建，需要安装 .NET 8.0 SDK（项目 `global.json` 指定 8.0.126）。

## 构建与安装

### 子模块初始化失败怎么办？

确保使用 `--recursive` 参数克隆，或手动初始化：

```bash
git submodule update --init --recursive
```

### `dotnet restore` 失败

检查网络连接和 NuGet 源配置。项目使用 `NuGet.Config` 文件配置包源，确保可以访问 nuget.org。如果在内网环境，可能需要配置代理或私有 NuGet 源。

### 构建时提示缺少模型文件

ONNX 模型文件位于 `Models/` 目录下，包括：

- `resnet18_equal_symbol_latest.onnx`
- `resnet18_operator_latest.onnx`
- `resnet34_digit_latest.onnx`

这些文件较大（42-81 MB），可能不在 Git 仓库中。有两种方式获取：

1. **自动下载** — 运行 OCR 服务或调用 `CasOcr.EnsureModelsAsync()` 时会自动从 Gitee Release 下载
2. **手动下载** — 从 `https://gitee.com/a645162/shmtu-cas-ocr-model/releases/download/v1.0-ONNX` 下载并放到 `Models/` 目录

## CAS 认证

### 登录失败怎么办？

常见原因：

1. **验证码识别失败** — 验证码识别准确率约 95%，偶尔会出现识别错误。应用通常会自动重试，一般 2-3 次内可以成功
2. **密码错误** — 确认学号和密码正确
3. **CAS 服务不可用** — 检查校园网连接，确认 CAS 服务正常运行
4. **会话过期** — 长时间未操作后需要重新认证，`EpayAuth` 会自动处理 TGC 复用和重新登录

### 验证码识别准确率是多少？

使用 ResNet 模型的 ONNX 推理，验证码识别准确率约 95% 以上。对于识别失败的情况，应用会自动重新获取验证码并重试，通常 2-3 次内可以成功。

### 如何选择验证码识别方式？

项目提供三种验证码解析器：

| 解析器 | 适用场景 | 说明 |
|--------|---------|------|
| `ManualCaptchaResolver` | 开发调试 | 手动在控制台输入验证码 |
| `RemoteOcrCaptchaResolver` | 高性能场景 | TCP 连接 OCR 服务，延迟最低 |
| `RemoteOcrHttpCaptchaResolver` | 通用场景 | HTTP 调用 OCR 服务，易于集成 |

如果不使用远程 OCR 服务，也可以直接使用 `shmtu-ocr-onnx-lib` 进行本地推理。

## OCR 服务

### OCR 服务启动失败

1. **端口被占用** — 更换端口号：`python3 Scripts/run_ocr_server.py run 8080`
2. **模型文件缺失** — 服务启动时会自动下载缺失模型，检查网络连接；也可手动下载放到 `Models/` 目录
3. **.NET 运行时缺失** — 安装 ASP.NET Core 8.0 运行时

### OCR 服务响应慢

- 检查 `PoolSize` 配置，适当增大推理池大小
- 首次请求会有模型加载开销，预热后速度会显著提升
- 考虑使用 GPU 加速：配置 `ExecutionProvider` 为 `CUDA` 并使用 GPU 构建条件编译
- 对于高频调用，TCP 协议比 HTTP 协议开销更小

### HTTP 和 TCP 两种接口有什么区别？

- **HTTP API** — 提供 RESTful 接口，支持健康检查、状态查询、Base64 和文件上传识别，适合低频调用和调试
- **TCP 协议** — 更轻量的二进制协议，发送图片字节 + `<END>` 标记即可接收结果，适合高频调用场景

两种接口内部共享同一个 `OcrService` 推理池，识别结果一致。

### 如何在其他项目中集成 OCR 功能？

有两种方式：

1. **本地集成** — 通过 NuGet 安装 `shmtu-ocr-onnx-lib` 包，直接在代码中调用 `CasOcr.PredictValidateCode()`
2. **远程服务** — 部署 `shmtu-ocr-onnx-server`，通过 HTTP API 或 TCP 协议调用

## 开发相关

### 如何运行测试？

```bash
# OCR 一致性测试
python3 Scripts/run_ocr_tests.py

# 或直接使用 dotnet test
dotnet test shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-tests
```

### 如何发布 NuGet 包？

使用 `shmtu-dotnet-lib/Scripts/push_nuget.py` 脚本发布。确保已配置 NuGet API Key。

### 项目使用什么架构模式？

桌面应用使用 MVVM（Model-View-ViewModel）模式，基于 Avalonia UI 框架：

- `Views/` — XAML 视图
- `ViewModels/` — 视图模型，处理业务逻辑
- `Services/` — 服务层，提供日志、依赖注入、加密、会话管理、数据导入导出等功能
- `Database/` — 数据访问层（SqlSugar ORM）
- `Models/` — 数据模型（账单、身份、配置等）

核心库遵循分层设计，将 CAS 认证（`cas/auth/`、`cas/captcha/`）、账单同步（`sync/`）、OCR 推理等功能独立封装，方便单独使用。

### 如何启用 GPU 加速？

使用 `UseGpu` 构建属性编译：

```bash
dotnet build -p:UseGpu=true
```

这会自动引用 `Microsoft.ML.OnnxRuntime.Gpu` 并启用 CUDA 执行提供程序。需要安装 NVIDIA GPU、CUDA 驱动和 cuDNN。
