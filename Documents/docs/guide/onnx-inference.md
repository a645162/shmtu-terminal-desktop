# ONNX推理

## 概述

本项目使用 ONNX Runtime 作为推理后端，基于 ResNet 卷积神经网络模型实现验证码的自动识别。推理引擎封装在 `shmtu-ocr-onnx-lib` 库中，支持本地推理和远程服务两种模式，同时支持 CPU 和 CUDA GPU 加速。

## 模型文件

项目使用三组 ONNX 模型，位于 `Models/` 目录下：

| 模型文件 | 用途 | 架构 |
|----------|------|------|
| `resnet18_equal_symbol_latest.onnx` | 等号/运算符识别 | ResNet18 |
| `resnet18_operator_latest.onnx` | 运算符分类（`+` / `-`） | ResNet18 |
| `resnet34_digit_latest.onnx` | 数字识别（0-9） | ResNet34 |

### 模型说明

- **ResNet18 等号识别模型**: 用于识别验证码中的等号符号位置，将表达式分为左右两部分。该模型同时区分中文式等号和符号式等号，决定后续字符分割的关键点位置
- **ResNet18 运算符模型**: 用于识别加减运算符（`+`、`-`）
- **ResNet34 数字模型**: 用于识别 0-9 的数字字符

三组模型协同工作，完成从验证码图像到数学表达式再到计算结果的完整流程。

### 模型下载

模型文件托管在 Gitee Release 上，`CasOcr` 提供 `EnsureModelsAsync()` 方法自动检测缺失模型并下载，下载时会验证 SHA256 校验和确保完整性：

```csharp
var ocr = new CasOcr();
await ocr.EnsureModelsAsync(
    progress: new Progress<float>(p => Console.WriteLine($"下载进度: {p}%")),
    log: msg => Console.WriteLine(msg));
```

下载源地址为：`https://gitee.com/a645162/shmtu-cas-ocr-model/releases/download/v1.0-ONNX`

## 推理流程

验证码识别的完整流程如下：

1. **图像预处理** — 对原始验证码图像进行灰度化、二值化（基于阈值 `ConfigThresh`）、去噪等预处理
2. **等号定位** — 使用 ResNet18 等号模型识别等号位置，确定表达式分割的关键点
3. **字符分割** — 根据等号模型确定的关键点（`KeyPoint`），将图像分割为数字1、运算符、数字2 三个区域
4. **运算符识别** — 使用 ResNet18 运算符模型识别 `+` 或 `-`
5. **数字识别** — 使用 ResNet34 数字模型分别识别两个数字
6. **表达式计算** — 将识别结果组合为数学表达式并计算最终结果

::: tip 等号类型
等号模型会区分两种等号样式：中文式（`Chs`）和符号式（`Symbol`），不同类型对应不同的分割关键点。这使得模型能适应不同风格的验证码图片。
:::

## 核心库代码结构

```
shmtu-ocr-onnx-lib/
├── CaptchaOcrLib.cs               # 库入口，版本管理
├── CasOcr.cs                      # CAS 验证码识别主入口（公开 API）
├── ConstValue.cs                  # 常量定义（模型文件名、下载地址）
├── ImageProcess/
│   ├── CasCaptchaImage.cs         # 验证码图像处理（分割、关键点定义）
│   ├── ImageUtils.cs              # 图像工具方法（二值化等）
│   └── ResNetProcess.cs           # ResNet 推理处理（图像转 Tensor）
├── Backend/
│   └── CasOnnxBackend.cs          # ONNX 推理后端（模型加载、推理、下载）
└── Utils/
    └── NetworkFile.cs             # 网络文件下载工具（SHA256 校验）
```

## 使用方式

### 方式一：本地推理

直接在应用中调用 OCR 库进行本地推理，无需外部服务：

```csharp
using shmtu.captcha.onnx;

// 创建识别器实例（可选指定模型目录、是否使用 GPU）
var ocr = new CasOcr(); // 默认使用 CPU，模型目录自动解析

// 确保模型文件存在（缺失则自动下载）
await ocr.EnsureModelsAsync();

// 加载模型到内存
ocr.LoadModel();

// 识别验证码图像（支持多种输入格式）
byte[] imageBytes = File.ReadAllBytes("captcha.png");
var (result, expr, equalSymbol, op, digit1, digit2) = ocr.PredictValidateCode(imageBytes);

Console.WriteLine($"表达式: {expr}");      // 如 "3 + 5 = 8"
Console.WriteLine($"结果: {result}");      // 如 8
Console.WriteLine($"运算符: {(op == 0 ? "+" : "-")}");
Console.WriteLine($"数字1: {digit1}, 数字2: {digit2}");
```

### CasOcr 构造函数

```csharp
// 默认配置：CPU 推理，模型目录自动解析
new CasOcr()

// 自定义模型目录
new CasOcr(modelDirectoryPath: "/path/to/models")

// 启用 CUDA GPU 加速（需使用 GPU_BUILD 编译条件）
new CasOcr(useGpu: true, gpuDeviceId: 0)

// 完整参数
new CasOcr(modelDirectoryPath: "/path/to/models", useGpu: true, gpuDeviceId: 0)
```

### PredictValidateCode 输入格式

`PredictValidateCode` 支持四种输入格式：

| 重载 | 输入类型 | 说明 |
|------|---------|------|
| `PredictValidateCode(SKBitmap)` | SKBitmap | 直接传入已解码的位图 |
| `PredictValidateCode(string)` | 文件路径 | 从文件加载图片 |
| `PredictValidateCode(Stream)` | 流 | 从流中读取图片 |
| `PredictValidateCode(byte[])` | 字节数组 | 从字节数组读取图片 |

返回值为元组 `(int Result, string Expr, int EqualSymbol, int Operator, int Digit1, int Digit2)`，识别失败时 Result 为 -1。

### 方式二：命令行工具

使用 OCR CLI 工具快速测试，支持 HTTP 和 TCP 两种连接方式：

```bash
# 通过辅助脚本启动
python3 Scripts/run_ocr_cli.py

# 或直接运行 dotnet 项目
dotnet run --project shmtu-dotnet-lib/ocr/shmtu-ocr-cli
```

### 方式三：GUI 演示

Avalonia 构建的图形界面演示工具，支持可视化查看识别过程：

```bash
python3 Scripts/run_ocr_demo_gui.py
```

## GPU 加速

ONNX 推理库支持 CUDA GPU 加速，需要使用 `GPU_BUILD` 编译条件：

```bash
# 构建 GPU 版本
dotnet build -p:UseGpu=true
```

当 `UseGpu=true` 时，项目会自动引用 `Microsoft.ML.OnnxRuntime.Gpu` 替代 `Microsoft.ML.OnnxRuntime`，并在推理时通过 `AppendExecutionProvider_CUDA` 启用 CUDA 加速。

::: warning GPU 前提条件
使用 GPU 加速需要：
- NVIDIA GPU 及正确安装的 CUDA 驱动
- cuDNN 库
- 仅在编译时通过 `GPU_BUILD` 条件编译启用，运行时无法切换
:::

## 推理性能

ONNX Runtime 在 CPU 上的推理性能参考：

| 模型 | 平均推理时间 | 说明 |
|------|-------------|------|
| ResNet18 等号识别 | ~5 ms | 单次推理 |
| ResNet18 运算符 | ~5 ms | 单次推理 |
| ResNet34 数字 | ~8 ms | 每个数字一次推理 |
| **完整验证码** | **~30-50 ms** | 含预处理和后处理 |

::: tip 性能优化
- 使用推理池（PoolSize）可以并发处理多个识别请求
- 首次推理会有模型加载开销，后续推理显著加快
- GPU 加速可进一步提升推理速度
:::

## 一致性测试

项目提供了 OCR 一致性测试工具，用于验证不同版本间的识别稳定性：

```bash
python3 Scripts/run_ocr_tests.py
```

测试位于 `shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-tests/` 目录，会自动生成验证码样本并批量测试识别准确率。
