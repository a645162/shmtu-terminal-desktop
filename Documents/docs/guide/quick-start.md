# 快速开始

## 项目简介

SHMTU Terminal Desktop 是上海海事大学校园终端桌面版应用，基于 .NET 8 和 Avalonia UI 框架构建。项目包含桌面应用本体和 `shmtu-dotnet-lib` 核心库两部分，提供 CAS 统一认证、校园卡账单同步、验证码 OCR 识别等功能。

## 项目结构

```
shmtu-terminal-desktop/
├── shmtu-terminal-desktop/     # Avalonia 桌面应用
│   ├── Views/                  # 界面视图
│   ├── ViewModels/             # 视图模型 (MVVM)
│   ├── Services/               # 服务层 (日志、定位等)
│   ├── Database/               # 数据库初始化
│   ├── Controls/               # 自定义控件
│   └── Models/                 # 数据模型
├── shmtu-dotnet-lib/           # 核心库 (Git 子模块)
│   ├── Core/
│   │   ├── shmtu-dotnet-lib/   # CAS认证、账单同步、分类器
│   │   │   ├── cas/            # CAS 认证与验证码
│   │   │   │   ├── auth/       #   EpayAuth、CasAuth、WechatAuth
│   │   │   │   └── captcha/    #   Captcha、ICaptchaResolver
│   │   │   ├── sync/           # 账单增量同步
│   │   │   ├── parser/         # HTML 解析
│   │   │   ├── Classifier/     # 账单分类器
│   │   │   └── utils/          # 工具方法
│   │   └── shmtu-dotnet-demo/  # 核心库演示程序
│   └── ocr/
│       ├── shmtu-ocr-onnx-lib/     # ONNX OCR 推理库
│       ├── shmtu-ocr-onnx-server/  # OCR HTTP/TCP 服务
│       ├── shmtu-ocr-onnx-demo/    # OCR 命令行演示
│       ├── shmtu-ocr-onnx-demo-gui/ # OCR GUI 演示 (Avalonia)
│       ├── shmtu-ocr-cli/          # OCR CLI 工具
│       └── shmtu-ocr-onnx-tests/   # OCR 一致性测试
├── Models/                     # ONNX 模型文件
└── Scripts/                    # 辅助脚本
```

## 环境要求

- **.NET SDK**: 8.0 或更高版本（项目 `global.json` 指定 8.0.126）
- **操作系统**: Windows 10+、Linux (x64/ARM64)、macOS 12+
- **IDE**: Visual Studio 2022、JetBrains Rider 或 VS Code + C# Dev Kit

## 快速构建

1. 克隆仓库（含子模块）：

```bash
git clone --recursive https://github.com/a645162/shmtu-terminal-desktop.git
cd shmtu-terminal-desktop
```

如果已经克隆但未初始化子模块：

```bash
git submodule update --init --recursive
```

2. 还原依赖并构建：

```bash
dotnet restore
dotnet build
```

3. 运行桌面应用：

```bash
dotnet run --project shmtu-terminal-desktop
```

4. 运行 CAS 演示程序：

```bash
python3 Scripts/run_dotnet_demo.py
```

## 使用辅助脚本

项目提供了多个 Python 辅助脚本，位于 `Scripts/` 目录下：

| 脚本 | 用途 |
|------|------|
| `run_dotnet_demo.py` | 运行 CAS 认证演示 |
| `run_ocr_server.py` | 启动 OCR HTTP 服务 |
| `run_ocr_demo.py` | 运行 OCR 命令行演示 |
| `run_ocr_demo_gui.py` | 运行 OCR GUI 演示 |
| `run_ocr_cli.py` | 运行 OCR CLI 工具 |
| `run_ocr_tests.py` | 运行 OCR 一致性测试 |
| `update.py` | 更新项目版本和依赖 |

例如启动 OCR 服务（端口 5000）：

```bash
python3 Scripts/run_ocr_server.py run 5000
```

## 下一步

- [安装与配置](./install-and-config) — 详细的安装步骤和配置说明
- [CAS认证](./cas-auth) — CAS 登录流程和 API 使用
- [ONNX推理](./onnx-inference) — 验证码识别引擎的工作原理和使用方法
- [OCR服务部署](./ocr-server-deploy) — 部署 OCR HTTP/TCP 服务的完整指南
