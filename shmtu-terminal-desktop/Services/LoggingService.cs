using System.IO;
using Serilog;
using Serilog.Events;

namespace shmtu.terminal.desktop.Services;

/// <summary>
/// 日志级别枚举 — 对应 Serilog 的 LogEventLevel
/// </summary>
public enum LogLevel
{
    Verbose = 0,   // Verbose: 最详细的跟踪信息
    Debug = 1,      // Debug: 开发调试信息
    Information = 2, // Information: 一般信息性消息
    Warning = 3,    // Warning: 警告但不影响功能
    Error = 4,      // Error: 错误导致功能异常
    Fatal = 5,      // Fatal: 致命错误导致程序崩溃
}

/// <summary>
/// 统一日志服务 — 基于 Serilog
/// 提供结构化日志输出，支持控制台和文件
/// </summary>
public static class LoggingService
{
    private static bool _isInitialized;
    private static string _logDirectory = "";

    /// <summary>
    /// 日志目录路径
    /// </summary>
    public static string LogDirectory => _logDirectory;

    /// <summary>
    /// 初始化日志系统 — 应在程序启动早期调用
    /// </summary>
    /// <param name="logDir">日志目录路径，默认为 Data/logs</param>
    /// <param name="minLevel">最小日志级别，默认 Debug</param>
    public static void Initialize(string? logDir = null, LogLevel minLevel = LogLevel.Debug)
    {
        if (_isInitialized) return;

        _logDirectory = logDir ?? Path.Combine(
            shmtu.terminal.desktop.Database.Common.BaseDbSource.DataDirectoryPath, "logs");

        // 确保日志目录存在
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }

        // 构建日志文件路径（按日期滚动）
        var logFilePath = Path.Combine(_logDirectory, "shmtu-terminal-.log");

        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(ConvertToSerilogLevel(minLevel))
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,  // 保留最近 30 天的日志
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        _isInitialized = true;

        Log.Information("日志系统初始化完成 | 目录: {LogDir} | 最小级别: {MinLevel}",
            _logDirectory, minLevel);
    }

    /// <summary>
    /// 关闭日志系统 — 应在程序退出时调用
    /// </summary>
    public static void Shutdown()
    {
        Log.Information("应用程序正在关闭");
        Log.CloseAndFlush();
    }

    #region 日志方法

    /// <summary>
    /// Verbose 日志 — 最详细的跟踪信息
    /// 用于记录详细的执行流程（如循环中的每次迭代）
    /// </summary>
    public static void Verbose(string messageTemplate, params object[] args)
    {
        Log.Verbose(messageTemplate, args);
    }

    /// <summary>
    /// Debug 日志 — 开发调试信息
    /// 用于记录开发时需要查看的详细信息（变量值、条件分支）
    /// </summary>
    public static void Debug(string messageTemplate, params object[] args)
    {
        Log.Debug(messageTemplate, args);
    }

    /// <summary>
    /// Information 日志 — 一般信息性消息
    /// 用于记录重要的业务操作（用户登录、数据同步开始/完成）
    /// </summary>
    public static void Information(string messageTemplate, params object[] args)
    {
        Log.Information(messageTemplate, args);
    }

    /// <summary>
    /// Warning 日志 — 警告但不影响功能
    /// 用于记录潜在问题（配置缺失使用默认值、重试成功）
    /// </summary>
    public static void Warning(string messageTemplate, params object[] args)
    {
        Log.Warning(messageTemplate, args);
    }

    /// <summary>
    /// Error 日志 — 错误导致功能异常
    /// 用于记录错误但不导致程序崩溃的情况（某个账号同步失败）
    /// </summary>
    public static void Error(Exception? ex, string messageTemplate, params object[] args)
    {
        if (ex != null)
            Log.Error(ex, messageTemplate, args);
        else
            Log.Error(messageTemplate, args);
    }

    /// <summary>
    /// Error 日志重载 — 无异常版本
    /// </summary>
    public static void Error(string messageTemplate, params object[] args)
    {
        Log.Error(messageTemplate, args);
    }

    /// <summary>
    /// Fatal 日志 — 致命错误导致程序崩溃
    /// 用于记录会导致应用崩溃的严重错误
    /// </summary>
    public static void Fatal(Exception? ex, string messageTemplate, params object[] args)
    {
        if (ex != null)
            Log.Fatal(ex, messageTemplate, args);
        else
            Log.Fatal(messageTemplate, args);
    }

    /// <summary>
    /// Fatal 日志重载 — 无异常版本
    /// </summary>
    public static void Fatal(string messageTemplate, params object[] args)
    {
        Log.Fatal(messageTemplate, args);
    }

    #endregion

    #region 结构化日志 — 业务场景专用

    /// <summary>
    /// 记录用户操作
    /// </summary>
    public static void LogUserAction(string action, int? identityId = null, string? accountId = null)
    {
        Log.Information("[用户操作] {Action} | IdentityId={IdentityId} | AccountId={AccountId}",
            action, identityId, accountId);
    }

    /// <summary>
    /// 记录数据库操作
    /// </summary>
    public static void LogDbOperation(string operation, string tableName, int? affectedRows = null)
    {
        Log.Debug("[数据库] {Operation} | Table={TableName} | Rows={AffectedRows}",
            operation, tableName, affectedRows);
    }

    /// <summary>
    /// 记录同步操作
    /// </summary>
    public static void LogSyncOperation(string accountId, int page, int newCount, string status)
    {
        Log.Information("[同步] AccountId={AccountId} | Page={Page} | NewCount={NewCount} | Status={Status}",
            accountId, page, newCount, status);
    }

    /// <summary>
    /// 记录配置加载
    /// </summary>
    public static void LogConfigLoad(string configName, bool success, string? detail = null)
    {
        if (success)
            Log.Debug("[配置] 加载成功 | Config={ConfigName} | {Detail}", configName, detail);
        else
            Log.Warning("[配置] 加载失败 | Config={ConfigName} | {Detail}", configName, detail);
    }

    /// <summary>
    /// 记录网络请求
    /// </summary>
    public static void LogNetworkRequest(string method, string url, int? statusCode = null)
    {
        Log.Debug("[网络] {Method} {Url} | Status={StatusCode}", method, url, statusCode);
    }

    #endregion

    #region 辅助方法

    private static Serilog.Events.LogEventLevel ConvertToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Verbose => Serilog.Events.LogEventLevel.Verbose,
            LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
            LogLevel.Information => Serilog.Events.LogEventLevel.Information,
            LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
            LogLevel.Error => Serilog.Events.LogEventLevel.Error,
            LogLevel.Fatal => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information,
        };
    }

    #endregion
}
