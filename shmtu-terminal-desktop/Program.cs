using System;
using Avalonia;
using Avalonia.ReactiveUI;
using shmtu.terminal.desktop.Services;

namespace shmtu.terminal.desktop;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 初始化日志系统（最早执行，以便记录启动过程）
        LoggingService.Initialize();
        LoggingService.Information("=== 应用程序启动 === | 命令行参数: {Args}",
            args.Length > 0 ? string.Join(" ", args) : "无");

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LoggingService.Fatal(ex, "应用程序发生未捕获的致命异常");
            throw;
        }
        finally
        {
            LoggingService.Shutdown();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        LoggingService.Debug("配置 Avalonia 应用构建器");
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}
