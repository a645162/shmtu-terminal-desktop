namespace shmtu.terminal.desktop.Models.Config;

/// <summary>
/// 全局应用配置模型 — 对应 app_config.toml
/// </summary>
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
    /// <summary>
    /// manual / remote_ocr / local_onnx
    /// </summary>
    public string Mode { get; set; } = "manual";
    public string RemoteOcrHost { get; set; } = "";
    public int RemoteOcrPort { get; set; } = 0;
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
    /// <summary>
    /// light / dark / system
    /// </summary>
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "zh-CN";
}
