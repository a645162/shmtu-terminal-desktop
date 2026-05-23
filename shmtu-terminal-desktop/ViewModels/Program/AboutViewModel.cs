using System.Reactive;
using ReactiveUI;

namespace shmtu.terminal.desktop.ViewModels.Program;

/// <summary>
/// ViewModel for the About window
/// XAML bindings: VersionText, OpenGitHubCommand, CheckUpdateCommand, ShowLicenseCommand
/// </summary>
public class AboutViewModel : ViewModelBase
{
    private string _versionText = "海大终端 v1.0.0";
    public string VersionText
    {
        get => _versionText;
        set => this.RaiseAndSetIfChanged(ref _versionText, value);
    }

    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckUpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowLicenseCommand { get; }

    public AboutViewModel()
    {
        OpenGitHubCommand = ReactiveCommand.Create(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/a645162",
                    UseShellExecute = true,
                });
            }
            catch { }
        });

        CheckUpdateCommand = ReactiveCommand.Create(() =>
        {
            // TODO: Implement update check
        });

        ShowLicenseCommand = ReactiveCommand.Create(() =>
        {
            // TODO: Show license dialog
        });

        // Try to get version from assembly
        try
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText = $"海大终端 v{version.Major}.{version.Minor}.{version.Build}";
            }
        }
        catch { }
    }
}
