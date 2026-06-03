# OCR服务部署

## 概述

`shmtu-ocr-onnx-server` 是一个基于 ASP.NET Core 构建的 OCR 服务，提供 HTTP 和 TCP 两种协议接口供外部应用调用验证码识别功能。服务内部使用 ONNX Runtime 进行推理，基于 `ObjectPool<CasOcr>` 实现推理池，支持并发请求处理和 CUDA GPU 加速。

## 服务架构

```
客户端请求
    │
    ├── HTTP ──────────────────────┐
    │   ├── GET  /api/health      │
    │   ├── GET  /api/status      │
    │   ├── POST /api/ocr         │
    │   └── POST /api/ocr/upload  │
    │                              ├──→ ASP.NET Core HTTP 服务器
    │                              │
    ├── TCP ───────────────────────┤
    │   (端口 21601, <END> 分隔)  ├──→ TcpOcrServerService (BackgroundService)
    │                              │
    └──────────────────────────────┘
                   │
                   ▼
           OcrService (单例)
                   │
                   ▼
      ONNX 推理池 ObjectPool<CasOcr> (可配置大小)
                   │
                   ▼
         ResNet 模型推理 (CPU / CUDA)
```

## 快速启动

### 使用辅助脚本

最简单的启动方式，使用项目提供的 Python 辅助脚本：

```bash
# 在项目根目录下
python3 Scripts/run_ocr_server.py run 5000
```

这将启动 OCR 服务并监听 5000 端口。

### 直接运行

```bash
cd shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-server
dotnet run
```

### 自定义端口

```bash
# 使用 --urls 参数指定监听地址
dotnet run --urls "http://0.0.0.0:8080"
```

或在 `appsettings.json` 中配置：

```json
{
  "Urls": "http://0.0.0.0:5000",
  "OcrServer": {
    "ServerName": "shmtu-ocr-server",
    "PoolSize": 4,
    "ExecutionProvider": "CPU",
    "TcpPort": 21601,
    "TcpListenAddress": "0.0.0.0"
  }
}
```

## 配置参数

| 参数 | 说明 | 默认值 | 建议值 |
|------|------|--------|--------|
| `PoolSize` | ONNX 推理池大小 | CPU 核心数（最小 4） | 根据 CPU 核心数设置，通常为核心数 |
| `ServerName` | 服务标识名称 | `shmtu-ocr-server` | 按需自定义 |
| `ModelDirectory` | 模型文件路径 | 自动解析（相对于可执行文件的 `../Models`） | 自定义路径时设置 |
| `ExecutionProvider` | 推理后端 | `CPU` | 使用 GPU 时设为 `CUDA` |
| `GpuDeviceId` | GPU 设备 ID | `0` | 多 GPU 时指定设备 |
| `TcpPort` | TCP 服务监听端口 | `21601` | 按需修改 |
| `TcpListenAddress` | TCP 服务监听地址 | `0.0.0.0` | 按需修改 |

::: warning 注意
- `PoolSize` 不宜设置过大，每个推理池实例都会占用一定的内存。建议根据服务器内存和 CPU 核心数合理配置
- `ExecutionProvider` 设为 `CUDA` 需要使用 GPU_BUILD 编译条件构建，且服务器必须有可用的 NVIDIA GPU 和 CUDA 驱动
:::

## HTTP API 接口

### 健康检查

```
GET /api/health
```

**响应示例：**

```json
{
  "status": "healthy",
  "modelsLoaded": true,
  "poolSize": 4,
  "serverName": "shmtu-ocr-server"
}
```

### 服务状态

```
GET /api/status
```

**响应示例：**

```json
{
  "status": "healthy",
  "availabilityLevel": "available",
  "reason": "",
  "modelsLoaded": true,
  "poolSize": 4,
  "queueCapacity": 4,
  "pendingRequests": 0,
  "activeWorkers": 4,
  "totalRequests": 1234,
  "successCount": 1220,
  "failureCount": 14,
  "uptimeSeconds": 86400,
  "serverName": "shmtu-ocr-server"
}
```

### Base64 图片识别

```
POST /api/ocr
Content-Type: application/json
```

**请求体：**

```json
{
  "imageBase64": "<base64编码的验证码图片>"
}
```

**响应示例：**

```json
{
  "success": true,
  "expression": "3 + 5 = 8",
  "result": 8,
  "equalSymbol": 0,
  "operator": 0,
  "digit1": 3,
  "digit2": 5,
  "error": null
}
```

### 文件上传识别

```
POST /api/ocr/upload
Content-Type: multipart/form-data
```

**请求参数：** `file` — 验证码图片文件

**响应示例：**

```json
{
  "success": true,
  "expression": "7 - 2 = 5",
  "result": 5,
  "equalSymbol": 1,
  "operator": 1,
  "digit1": 7,
  "digit2": 2,
  "error": null
}
```

### 响应字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | bool | 识别是否成功 |
| `expression` | string | 完整表达式（如 `3 + 5 = 8`） |
| `result` | int | 计算结果 |
| `equalSymbol` | int | 等号类型索引 |
| `operator` | int | 运算符索引（0=加, 1=减） |
| `digit1` | int | 第一个数字 |
| `digit2` | int | 第二个数字 |
| `error` | string? | 错误信息，成功时为 null |

## TCP 协议接口

除了 HTTP API，服务还提供 TCP 协议的 OCR 接口，适用于高性能场景。

### 协议格式

1. 客户端连接到 TCP 端口（默认 21601）
2. 发送验证码图片的原始字节数据
3. 在数据末尾追加 `<END>` 标记（UTF-8 编码）
4. 服务端识别完成后返回表达式文本（如 `3 + 5 = 8`）
5. 识别失败时返回空字符串
6. 连接超时时间为 30 秒

### 客户端示例

核心库已内置 TCP OCR 客户端方法，可直接使用：

```csharp
using shmtu.cas.captcha;

// 同步调用
string result = Captcha.OcrByRemoteTcpServer("127.0.0.1", 21601, imageBytes);

// 异步调用
string result = await Captcha.OcrByRemoteTcpServerAsync("127.0.0.1", 21601, imageBytes);
```

手动连接示例（任何语言均可实现）：

```
1. TCP 连接到 127.0.0.1:21601
2. 发送: [image_bytes]<END>
3. 接收: "3 + 5 = 8"
4. 关闭连接
```

::: tip TCP vs HTTP
TCP 协议开销更小，适合高频调用场景。HTTP API 更易于集成和调试，适合低频调用。两者内部共享同一个 `OcrService` 推理池。
:::

## 服务初始化流程

服务启动时会自动执行以下初始化步骤：

1. **检测模型文件** — 检查模型目录下是否存在所有必需的 `.onnx` 文件
2. **下载缺失模型** — 缺失的模型文件会自动从 Gitee Release 下载，并验证 SHA256 校验和
3. **加载模型** — 将模型加载到 ONNX Runtime 推理会话中
4. **预热推理池** — 预先创建 `PoolSize` 个 `CasOcr` 实例并加载模型，消除首次请求延迟

初始化完成后，`/api/health` 的 `modelsLoaded` 字段会变为 `true`。

## 生产部署

### 使用 Systemd（Linux）

创建 `/etc/systemd/system/shmtu-ocr.service`：

```ini
[Unit]
Description=SHMTU OCR Server
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/shmtu-ocr
ExecStart=/usr/bin/dotnet /opt/shmtu-ocr/shmtu-ocr-onnx-server.dll --urls http://0.0.0.0:5000
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

启动服务：

```bash
sudo systemctl enable shmtu-ocr
sudo systemctl start shmtu-ocr
```

### 使用 Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY publish/ .
EXPOSE 5000
ENTRYPOINT ["dotnet", "shmtu-ocr-onnx-server.dll", "--urls", "http://0.0.0.0:5000"]
```

构建并运行：

```bash
# 发布项目
dotnet publish shmtu-dotnet-lib/ocr/shmtu-ocr-onnx-server -c Release -o publish

# 构建 Docker 镜像
docker build -t shmtu-ocr-server .

# 运行容器
docker run -d -p 5000:5000 -p 21601:21601 --name shmtu-ocr shmtu-ocr-server
```

::: warning Docker 端口映射
如果需要使用 TCP 协议接口，记得同时映射 TCP 端口（默认 21601）。
:::

### 反向代理（Nginx）

```nginx
server {
    listen 80;
    server_name ocr.example.com;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## 监控与日志

服务内置了详细的请求日志，记录每次 OCR 请求的处理时间和结果：

- 每次请求记录 Method、Path、RemoteIp、ContentLength
- 请求完成后记录 StatusCode、ElapsedMs
- 请求失败时记录异常详情
- TCP 连接记录 Remote、Bytes、Expression、ElapsedMs

通过 `GET /api/status` 可以获取运行时的统计数据，包括总请求数、成功数、失败数和运行时间。

## 与 CAS 认证集成

在 CAS 认证场景中，可以通过以下方式集成 OCR 服务：

**HTTP 方式**（使用 `RemoteOcrHttpCaptchaResolver`）：

```csharp
var resolver = new RemoteOcrHttpCaptchaResolver("http://127.0.0.1:5000");
var epay = new EpayAuth(resolver);
```

**TCP 方式**（使用 `RemoteOcrCaptchaResolver`）：

```csharp
var resolver = new RemoteOcrCaptchaResolver("127.0.0.1", 21601);
var epay = new EpayAuth(resolver);
```

详见 [CAS认证](./cas-auth) 文档中的验证码解析器部分。
