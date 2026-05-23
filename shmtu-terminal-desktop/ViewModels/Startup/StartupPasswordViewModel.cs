using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Security;

namespace shmtu.terminal.desktop.ViewModels.Startup;

/// <summary>
/// ViewModel for the Startup Password window
/// XAML bindings: Password, ErrorMessage, HasError, ConfirmCommand
/// </summary>
public class StartupPasswordViewModel : ViewModelBase
{
    private string _password = "";
    public string Password
    {
        get => _password;
        set => this.RaiseAndSetIfChanged(ref _password, value);
    }

    private string _errorMessage = "";
    public new string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }

    /// <summary>
    /// Event raised when password is successfully verified
    /// </summary>
    public event Action? PasswordVerified;

    public StartupPasswordViewModel()
    {
        ConfirmCommand = ReactiveCommand.Create(VerifyPassword);
    }

    private void VerifyPassword()
    {
        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "请输入密码";
            return;
        }

        var config = TomlConfigService.LoadAppConfig();

        if (!config.Security.EnableStartupProtection)
        {
            // No protection enabled, proceed
            PasswordVerified?.Invoke();
            return;
        }

        var inputHash = EncryptionService.HashPassword(Password);
        if (inputHash == config.Security.PasswordHash)
        {
            // Set master key for database encryption
            EncryptionService.SetMasterKey(Password);
            PasswordVerified?.Invoke();
        }
        else
        {
            ErrorMessage = "密码错误";
        }
    }
}
