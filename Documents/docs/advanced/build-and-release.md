# 构建与发布

本节讲解本地开发、文档构建、平台打包与 CI 工作流。

## 本地开发

### 仅桌面应用

```bash
# 还原依赖
dotnet restore shmtu-terminal-desktop.sln

# 编译
dotnet build shmtu-terminal-desktop.sln -c Debug

# 运行（Debug 模式含 Avalonia.Diagnostics）
dotnet run --project shmtu-terminal-desktop -c Debug

# 静态检查
dotnet build shmtu-terminal-desktop.sln -c Release -p:TreatWarningsAsErrors=true
```

### 含 OCR 服务

```bash
# 启动 ONNX 服务
cd shmtu-dotnet-lib/ocr
docker compose -f docker-compose.yml up -d

# 验证
curl http://127.0.0.1:5000/health
```

### 含 demo / 测试

```bash
# 控制台 demo
dotnet run --project shmtu-dotnet-lib/Core/shmtu-dotnet-demo

# ONNX demo
dotnet run --project shmtu-dotnet-lib/ocr/shmtu-ocr-cli -- --help
dotnet run --project shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-demo
```

## 文档构建

文档基于 **VitePress 2**。

```bash
# 安装依赖（首次）
npm install

# 本地预览
npm run docs:dev          # 默认 http://localhost:5173

# 生产构建
npm run docs:build        # 输出到 Documents/docs/.vitepress/dist

# 预览构建结果
npm run docs:preview      # 默认 http://localhost:4173
```

> VitePress 配置位于 `Documents/docs/.vitepress/config.ts`，通过 `resolveBase()` 自动适配 GitHub Pages 子路径。

## 平台打包

### Windows

```bash
dotnet publish shmtu-terminal-desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# 输出：bin/Release/net8.0/win-x64/publish/shmtu-terminal-desktop.exe
```

可选 `-p:PublishTrimmed=true` 减小体积（注意 Avalonia 不完全支持剪裁）。

### Linux

```bash
dotnet publish shmtu-terminal-desktop -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
chmod +x bin/Release/net8.0/linux-x64/publish/shmtu-terminal-desktop
```

### macOS

```bash
dotnet publish shmtu-terminal-desktop -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
# Apple Silicon
dotnet publish shmtu-terminal-desktop -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

### 多 RID 批量打包

`Scripts/` 下的 PowerShell 脚本：

```bash
pwsh nuget_package.ps1   # 构建 NuGet 包
pwsh Scripts/build_all.ps1  # 跨平台构建（需配置）
```

## NuGet 发布

```csharp
# 1. 设置 API key
dotnet nuget push API_KEY --source https://api.nuget.org/v3/index.json

# 2. 打 tag → CI 自动构建并推送
git tag v2.0.0
git push origin v2.0.0
```

`shmtu-dotnet-lib` 发布到 [nuget.org/packages/shmtu-dotnet-lib](https://www.nuget.org/packages/shmtu-dotnet-lib)：

```toml
[package]
id = "shmtu-dotnet-lib"
version = "2.0.0.1"
license = "GPL-3.0-only"
dependencies = ["Flurl.Http >=4.0.2", "HtmlAgilityPack >=1.12.4"]
```

## GitHub Actions

### 工作流列表

| Workflow | 触发 | 作用 |
|---|---|---|
| `dotnet.yml` | push / PR | 还原 + 构建 + 测试 + NuGet 打包 |
| `docker-publish.yml` | tag `v*` | 构建 OCR Server Docker 镜像并推 GHCR |
| `docs-pages.yml` | push main / 手动 | 部署文档到 GitHub Pages |

### docs-pages.yml

```yaml
name: Deploy User Docs
on:
  push:
    branches: [main, master]
  workflow_dispatch:
permissions:
  contents: read
  pages: write
  id-token: write
concurrency:
  group: pages
  cancel-in-progress: false
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-node@v6
        with: { node-version: 24, cache: npm }
      - uses: actions/configure-pages@v6
      - run: npm ci
      - run: npm run docs:build
      - uses: actions/upload-pages-artifact@v5
        with: { path: Documents/docs/.vitepress/dist }
  deploy:
    environment: { name: github-pages }
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/deploy-pages@v5
```

### 启用 GitHub Pages

1. 仓库 **Settings → Pages**
2. **Source**: GitHub Actions
3. 推送 `main` 触发部署，几分钟后访问 `https://<user>.github.io/shmtu-terminal-desktop/`

## 版本号策略

- **桌面应用**：`1.0.x` SemVer，发布到 GitHub Releases
- **`shmtu-dotnet-lib`**：`2.0.x.y` SemVer，发布到 NuGet
- **OCR 模型**：跟随应用版本，下载源在 `scripts/download_github_release.py`

## 下一步

回到 [高级文档总览](/advanced/overview) 查阅其他主题。
