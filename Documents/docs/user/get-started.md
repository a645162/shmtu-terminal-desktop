# 第一次使用

欢迎使用海大终端 .NET 桌面版。本指南会用 5 步带你完成从安装到第一份账单。

## 步骤 1：安装 .NET 8 运行时

应用基于 .NET 8 构建。打开 [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) 下载并安装 **.NET 8 Desktop Runtime**。

安装后在终端验证：

```bash
dotnet --list-runtimes
```

应当看到 `Microsoft.WindowsDesktop.App 8.x.x`（Windows）或 `Microsoft.AspNetCore.App 8.x.x`（Linux/macOS）。

## 步骤 2：下载并启动应用

从 [GitHub Releases](https://github.com/a645162/shmtu-terminal-desktop/releases) 下载对应平台的压缩包，解压后双击 `shmtu-terminal-desktop` 启动。Linux 用户可能需要赋予可执行权限：

```bash
chmod +x shmtu-terminal-desktop
./shmtu-terminal-desktop
```

也可以从源码启动：

```bash
git clone --recurse-submodules https://github.com/a645162/shmtu-terminal-desktop.git
cd shmtu-terminal-desktop
dotnet run --project shmtu-terminal-desktop
```

## 步骤 3：创建身份与账号

应用首次启动会进入**启动密码**设置页 — 这是一个本地密码，所有本地数据都通过它派生密钥加密，请妥善保管。

进入主界面后：

1. 点击 **身份管理** → **新建身份**，输入显示名（例如"我"）。
2. 在该身份下点击 **添加账号**，填入学号、密码和显示名。
3. 验证码模式先选**手动输入**，OCR 模式留待稍后开启。

## 步骤 4：（可选）启动 OCR 服务

如果不想每次输入验证码，可以启动本地 ONNX OCR 服务：

```bash
python3 Scripts/run_ocr_server.py run 5000
```

服务监听 `http://127.0.0.1:5000` 后，再到 **设置 → 验证码** 中切换为 **远程 OCR**。

## 步骤 5：第一次同步

回到主界面 → 选中刚添加的账号 → 点击 **同步**。首次同步会拉取历史数据，可能耗时 10-30 秒。

同步完成后，进入 **账单** 页查看消费记录，或到 **首页** 看汇总统计。

## 下一步

- [怎么同步账单](/user/sync-bills) — 增量/全量的区别
- [怎么看统计](/user/check-stats) — 首页和统计页的解读
- [怎么备份恢复](/user/backup-and-restore) — 重要数据记得备份
