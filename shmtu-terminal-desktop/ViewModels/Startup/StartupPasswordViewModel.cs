using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Config;
using shmtu.terminal.desktop.Services.Security;

namespace shmtu.terminal.desktop.ViewModels.Startup;

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

    public event Action? PasswordVerified;

    public StartupPasswordViewModel()
    {
        ConfirmCommand = ReactiveCommand.Create(VerifyPassword);
    }

    /// <summary>
    /// Verify the entered password and raise PasswordVerified on success.
    /// Supports both old SHA-256 hash format and new PBKDF2 format.
    /// </summary>
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
            PasswordVerified?.Invoke();
            return;
        }

        var storedHash = config.Security.PasswordHash;

        bool match;
        if (storedHash.Length == 64) // Legacy SHA-256 hex format (64 chars)
        {
            match = EncryptionService.VerifyPasswordHash(Password, storedHash);
        }
        else // New PBKDF2 Base64 format
        {
            match = EncryptionService.VerifyPasswordHash(Password, storedHash);
        }

        if (match)
        {
            EncryptionService.SetMasterKey(Password);
            PasswordVerified?.Invoke();
        }
        else
        {
            ErrorMessage = "密码错误";
        }
    }
}
