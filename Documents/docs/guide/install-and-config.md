# 安装与配置

## 系统要求

### 运行时要求

| 组件 | 最低版本 | 说明 |
|------|---------|------|
| .NET Runtime | 8.0 | 桌面应用和 OCR 服务均依赖（项目 `global.json` 指定 8.0.126） |
| ASP.NET Core | 8.0 | OCR HTTP 服务需要 |
| Avalonia | 12.0 | 桌面 UI 框架，随 NuGet 包自动安装 |

### 开发环境要求

- **.NET SDK 8.0+**: 编译和运行项目
- **NuGet**: 包管理（随 SDK 自带）
- **Python 3.8+**: 运行辅助脚本（可选）

## 安装方式

### 方式一：从源码构建

```bash
# 克隆并初始化子模块
git clone --recursive https://github.com/a645162/shmtu-terminal-desktop.git
cd shmtu-terminal-desktop

# 还原 NuGet 包
dotnet restore

# 编译整个解决方案
dotnet build

# 发布桌面应用（Release 模式）
dotnet publish shmtu-terminal-desktop -c Release -r win-x64 --self-contained
```

### 方式二：通过 NuGet 安装核心库

如果只需要 CAS 认证或 OCR 功能，可以单独安装 NuGet 包：

```bash
# 安装核心库（CAS认证、账单同步）
dotnet add package shmtu-dotnet-lib

# 安装 ONNX OCR 库
dotnet add package shmtu-ocr-onnx-lib
```

## 配置文件

### OCR 服务配置

OCR HTTP/TCP 服务使用 `appsettings.json` 进行配置：

```json
{
  "Urls": "http://0.0.0.0:5000",
  "OcrServer": {
    "ServerName": "shmtu-ocr-server",
    "PoolSize": 4,
    "ModelDirectory": "",
    "ExecutionProvider": "CPU",
    "GpuDeviceId": 0,
    "TcpPort": 21601,
    "TcpListenAddress": "0.0.0.0"
  }
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `ServerName` | 服务名称，用于标识 | `shmtu-ocr-server` |
| `PoolSize` | ONNX 推理池大小 | CPU 核心数（最小 4） |
| `ModelDirectory` | 模型文件路径，留空则自动解析 | 空 |
| `ExecutionProvider` | 推理后端，可选 `CPU` 或 `CUDA` | `CPU` |
| `GpuDeviceId` | GPU 设备 ID（多 GPU 时使用） | `0` |
| `TcpPort` | TCP 服务监听端口 | `21601` |
| `TcpListenAddress` | TCP 服务监听地址 | `0.0.0.0` |

### 桌面应用配置

桌面应用使用 Avalonia 的标准配置方式，数据库文件自动在用户数据目录下创建。应用首次启动时会自动完成数据库初始化（参见 `Database/InitDb.cs`）。

桌面应用使用 TOML 格式的配置文件（通过 `TomlConfigService` 管理），支持：

- 应用基本设置
- 账单分类规则配置
- 同步相关参数

## 跨平台支持

### Windows

```bash
# 构建并运行
dotnet run --project shmtu-terminal-desktop

# 发布为独立应用
dotnet publish shmtu-terminal-desktop -c Release -r win-x64 --self-contained
```

### Linux

```bash
# 需要安装相关依赖
# Ubuntu/Debian:
sudo apt-get install libicu-dev

# 构建并运行
dotnet run --project shmtu-terminal-desktop

# 发布
dotnet publish shmtu-terminal-desktop -c Release -r linux-x64 --self-contained
```

### macOS

```bash
dotnet run --project shmtu-terminal-desktop
dotnet publish shmtu-terminal-desktop -c Release -r osx-x64 --self-contained
```

## GPU 构建说明

如需使用 CUDA GPU 加速 OCR 推理，需要以 GPU 构建条件编译：

```bash
# 构建 GPU 版本的 OCR 库
dotnet build shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-lib -p:UseGpu=true

# 构建 GPU 版本的 OCR 服务
dotnet build shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-server -p:UseGpu=true
```

GPU 构建会自动将 ONNX Runtime 依赖从 CPU 版本切换为 GPU 版本（`Microsoft.ML.OnnxRuntime.Gpu`），并在运行时启用 CUDA 执行提供程序。

前提条件：
- NVIDIA GPU 及 CUDA 驱动
- cuDNN 库
- 与 ONNX Runtime GPU 版本兼容的 CUDA Toolkit

## NuGet 配置

项目包含自定义 NuGet 源配置文件 `NuGet.Config`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

如果需要在 CI 环境中发布包，请确保正确配置 NuGet API Key。
