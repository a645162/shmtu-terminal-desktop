namespace shmtu.terminal.desktop.Models.Config;

public class AppConfig
{
    public SecurityConfig Security { get; set; } = new();
    public IdentityConfig Identity { get; set; } = new();
    public CaptchaConfig Captcha { get; set; } = new();
    public SyncConfig Sync { get; set; } = new();
    public DataConfig Data { get; set; } = new();
    public ClassificationConfig Classification { get; set; } = new();
    public UpdateConfig Update { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
    public SemesterConfig Semester { get; set; } = new();
    public SessionConfig Session { get; set; } = new();
}

public class SecurityConfig
{
    public bool EnableStartupProtection { get; set; } = false;
    public string PasswordHash { get; set; } = "";
}

public class IdentityConfig
{
    public bool RememberDefault { get; set; } = false;
    public int DefaultIdentityId { get; set; } = 0;
}

public class CaptchaConfig
{
    /// <summary>manual / remote_ocr / remote_ocr_http / local_onnx</summary>
    public string Mode { get; set; } = "manual";
    public string RemoteOcrHost { get; set; } = "";
    public int RemoteOcrPort { get; set; } = 0;
    public string RemoteOcrHttpUrl { get; set; } = "http://127.0.0.1:5000";
    public string OnnxModelPath { get; set; } = "";
    public int OcrRetryCount { get; set; } = 3;
}

public class SyncConfig
{
    public int MaxPages { get; set; } = 100;
    public int EarlyStopThreshold { get; set; } = 5;
    public bool AutoMergeAfterSync { get; set; } = true;
}

public class DataConfig
{
    public string DataDirectory { get; set; } = "Data";
    public int SnapshotKeepCount { get; set; } = 10;
}

public class ClassificationConfig
{
    public string RulesPath { get; set; } = "";
    public string RulesUpdateUrl { get; set; } = "";
}

public class UpdateConfig
{
    public bool AutoCheck { get; set; } = true;
    public int CheckIntervalHours { get; set; } = 24;
    public string LastCheckTime { get; set; } = "";
}

public class UiConfig
{
    /// <summary>light / dark / system</summary>
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "zh-CN";
}

/// <summary>
/// HIGH 13: 学期配置 — 从配置文件读取学期日期
/// </summary>
public class SemesterConfig
{
    public string SemesterName { get; set; } = "2024-2025学年第二学期";
    public DateTime StartDate { get; set; } = new(2025, 2, 17);
    public DateTime EndDate { get; set; } = new(2025, 6, 27);
}

/// <summary>
/// Session 会话配置 — 控制 session 续期和过期检查行为
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// Session 续期检查间隔（分钟），默认 10 分钟
    /// 每次检查会上下浮动 1 分钟，避免同时检查
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// 是否启用自动 session 续期
    /// </summary>
    public bool AutoRefresh { get; set; } = true;
}
