# CAS认证

## 概述

上海海事大学使用 CAS（Central Authentication Service）统一认证系统进行身份验证。本项目的 `shmtu-dotnet-lib` 核心库封装了完整的 CAS 登录流程，采用原子化 API 设计，将登录拆分为 4 个独立步骤，调用方可灵活组合和重试。

## CAS 登录流程

CAS 认证的完整流程如下：

1. **探测登录状态** — 请求目标服务，检查是否已登录（返回 200 = 已登录，302 = 需要登录）
2. **获取登录参数** — 请求 CAS 登录页面，解析出 `execution` 令牌
3. **获取验证码** — 请求验证码图片接口，获取验证码图像
4. **识别验证码** — 将验证码图像送入 OCR 引擎识别（详见 [ONNX推理](./onnx-inference)）
5. **提交认证** — 将用户名、密码、验证码和登录参数一起提交到 CAS
6. **跟随重定向** — CAS 认证成功后返回 302，跟随重定向链建立目标站点会话
7. **验证结果** — 检查认证是否成功，确认目标站点可正常访问

::: tip TGC 自动复用
如果 TGC（Ticket Granting Cookie）仍然有效，CAS 会在第 2 步自动认证并返回 302 重定向，无需提交登录表单。`EpayAuth` 会自动处理这种情况。
:::

## 核心代码结构

CAS 认证相关代码位于 `shmtu-dotnet-lib/Core/shmtu-dotnet-lib/cas/` 目录下：

```
cas/
├── auth/
│   ├── common/
│   │   ├── CasAuth.cs           # CAS 登录原子操作（获取 execution、提交登录、跟随重定向）
│   │   ├── CasAuthResult.cs     # 登录结果（Success / ValidateCodeError / PasswordError / Failure）
│   │   ├── CasHttpClient.cs     # 共享 HttpClient（自动管理 Cookie）
│   │   ├── LoginChallenge.cs    # 登录挑战（execution + 验证码图片）
│   │   ├── LoginProbe.cs        # 探测结果（AlreadyLoggedIn / NeedLogin）
│   │   └── LoginSubmitResult.cs # 提交结果（Success / ValidateCodeError / PasswordError / Failure）
│   ├── EpayAuth.cs              # ePay 一卡通认证（4 步原子 API）
│   └── WechatAuth.cs            # 微信企业号认证
└── captcha/
    ├── Captcha.cs               # 验证码工具（拉取图片、远端 TCP/HTTP OCR、算式求值）
    ├── ICaptchaResolver.cs      # 验证码解析器接口
    ├── ManualCaptchaResolver.cs # 手动输入验证码
    ├── RemoteOcrCaptchaResolver.cs   # 远端 TCP OCR 解析
    ├── RemoteOcrHttpCaptchaResolver.cs # 远端 HTTP OCR 解析
    ├── CaptchaAnswer.cs         # 验证码答案
    └── CaptchaAnswerKind.cs     # 答案类型（Expression / RawText）
```

## EpayAuth 四步原子 API

`EpayAuth` 是面向 ePay（一卡通）场景的高级封装，将登录拆为 4 个原子方法，重试循环由调用方控制：

### 1. ProbeLoginAsync — 探测登录状态

```csharp
var probe = await epay.ProbeLoginAsync(cancellationToken);
// probe 是 LoginProbe 类型的 discriminated union：
//   - LoginProbe.AlreadyLoggedIn  → 已登录，无需操作
//   - LoginProbe.NeedLogin(url)   → 需要登录，url 是 CAS 登录地址
```

### 2. PrepareChallengeAsync — 获取登录挑战

```csharp
var challenge = await epay.PrepareChallengeAsync(cancellationToken);
// challenge.Execution  → CAS 登录所需的 execution 令牌
// challenge.CaptchaImage → byte[] 验证码图片数据
```

### 3. SubmitLoginAsync — 提交登录

```csharp
// 方式 A：完整流程（准备挑战 + 解验证码 + 提交），一行搞定
var result = await epay.SubmitLoginAsync("学号", "密码", cancellationToken);

// 方式 B：分步提交（外部已解出验证码）
var result = await epay.SubmitLoginAsync(
    "学号", "密码", "8", challenge.Execution, cancellationToken);
```

### 4. TestLoginStatusAsync — 验证登录状态

```csharp
var isLoggedIn = await epay.TestLoginStatusAsync(cancellationToken);
// 返回 true 表示已登录，可以访问 ePay 页面
```

### LoginSubmitResult 结果类型

| 结果类型 | 含义 |
|---------|------|
| `LoginSubmitResult.Success` | 登录成功 |
| `LoginSubmitResult.ValidateCodeError` | 验证码错误，可重试 |
| `LoginSubmitResult.PasswordError` | 用户名或密码错误 |
| `LoginSubmitResult.Failure(message)` | 其他错误 |

## 验证码解析器（ICaptchaResolver）

项目使用策略模式处理验证码识别，通过 `ICaptchaResolver` 接口抽象不同的识别方式：

```csharp
public interface ICaptchaResolver
{
    Task<CaptchaAnswer> ResolveAsync(byte[] captchaImage, CancellationToken ct = default);
}
```

### 内置解析器

| 解析器 | 用途 | 说明 |
|--------|------|------|
| `ManualCaptchaResolver` | 手动输入 | 用户在控制台输入验证码答案 |
| `RemoteOcrCaptchaResolver` | 远端 TCP OCR | 连接 TCP OCR 服务（默认端口 21601），发送图片接收识别结果 |
| `RemoteOcrHttpCaptchaResolver` | 远端 HTTP OCR | 调用 HTTP OCR 服务的 `/api/ocr` 接口 |

### CaptchaAnswer 结构

```csharp
public class CaptchaAnswer
{
    public CaptchaAnswerKind Kind; // Expression 或 RawText
    public string Value;           // 识别结果文本
}
```

- `Kind = Expression`：值为数学表达式（如 `3+5=8`），系统会自动提取等号右侧的计算结果
- `Kind = RawText`：值为原始文本，直接作为验证码答案使用

### 使用示例

```csharp
using shmtu.cas.auth;
using shmtu.cas.captcha;

// 选择验证码解析方式
ICaptchaResolver resolver = new RemoteOcrCaptchaResolver("127.0.0.1", 21601);
// 或：new RemoteOcrHttpCaptchaResolver("http://127.0.0.1:5000");
// 或：new ManualCaptchaResolver();

var epay = new EpayAuth(resolver);

// 探测 → 提交登录（内部自动处理验证码）
var probe = await epay.ProbeLoginAsync();
if (probe is LoginProbe.NeedLogin)
{
    var result = await epay.SubmitLoginAsync("学号", "密码");
    if (result is LoginSubmitResult.Success)
    {
        Console.WriteLine("登录成功");
    }
}
```

## CasAuth 底层操作

如果需要更细粒度的控制，可以直接使用 `CasAuth` 静态类的底层方法：

| 方法 | 作用 |
|------|------|
| `CasAuth.GetExecutionString(client, url)` | 拉取登录页，提取 `execution` 令牌。若 TGC 有效则返回空字符串 |
| `CasAuth.CasLogin(client, url, username, password, validateCode, execution)` | 提交登录表单，返回 `CasAuthResult` |
| `CasAuth.CasRedirect(client, url)` | 跟随 CAS 认证后的重定向链（最多 10 次），建立目标站点会话 |

## 验证码类型

CAS 系统使用的验证码为数学表达式类型，例如 `3+5=?`，需要用户计算后输入结果。验证码图片具有以下特征：

- 包含噪声和干扰线
- 字符可能有旋转和变形
- 表达式为加减法运算
- 结果为个位数或两位数

验证码拉取地址为 `https://cas.shmtu.edu.cn/cas/captcha`，Cookie 由 `CasHttpClient` 的 `CookieContainer` 自动管理。

OCR 引擎会自动将表达式识别并计算，返回最终数字结果。对于识别失败的情况，应用通常会自动重新获取验证码并重试，一般 2-3 次内可以成功。

## 运行 CAS 演示

使用辅助脚本快速测试 CAS 认证流程：

```bash
python3 Scripts/run_dotnet_demo.py
```

该脚本会启动 `shmtu-dotnet-demo` 项目，演示完整的 CAS 登录和账单查询流程。演示代码位于 `shmtu-dotnet-lib/Core/shmtu-dotnet-demo/`，包含 `CaptchaDemo.cs`（验证码识别）和 `BillDemo.cs`（账单同步）两个示例。

## 认证会话管理

CAS 认证成功后，系统会维护一个有效的会话（Session），基于 TGC Cookie 工作：

- **TGC（Ticket Granting Cookie）**: CAS 服务器颁发的全局会话凭证，存储在 `CasHttpClient` 的 `CookieContainer` 中
- **ST（Service Ticket）**: 针对特定服务的票据，一次性使用，CAS 自动通过重定向交换
- **会话有效期**: 由 CAS 服务器配置决定，通常为 2 小时

`EpayAuth.TryReuseTgcAsync()` 方法可以在登录前检查 TGC 是否仍有效，若有效则跳过登录表单提交，直接复用已有会话。

## 安全注意事项

- 密码在内存中以安全方式处理，不会明文持久化存储
- CAS 会话 Cookie 应妥善保管，避免泄露
- 在生产环境中建议使用 HTTPS 传输认证数据
- OCR 识别结果需要验证格式合法性，防止注入攻击
