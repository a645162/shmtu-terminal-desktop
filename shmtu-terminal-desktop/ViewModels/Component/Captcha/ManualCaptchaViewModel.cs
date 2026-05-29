using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System.Reactive.Linq;

namespace shmtu.terminal.desktop.ViewModels.Component.Captcha;

public class CaptchaResult
{
    public string Expression { get; set; } = "";
    public string Answer { get; set; } = "";
    public bool Success { get; set; }
}

/// <summary>
/// ViewModel for the manual captcha input dialog
/// </summary>
public class ManualCaptchaViewModel : ViewModelBase
{
    private string _captchaAnswer = "";
    public string CaptchaAnswer
    {
        get => _captchaAnswer;
        set
        {
            this.RaiseAndSetIfChanged(ref _captchaAnswer, value);
            if (!string.IsNullOrWhiteSpace(value) && HasError)
                ErrorMessage = "";
        }
    }

    private string _captchaExpression = "";
    public string CaptchaExpression
    {
        get => _captchaExpression;
        set => this.RaiseAndSetIfChanged(ref _captchaExpression, value);
    }

    /// <summary>
    /// Store raw image bytes for display
    /// </summary>
    internal byte[]? ImageBytes { get; private set; }

    /// <summary>
    /// Set the captcha image from bytes
    /// </summary>
    public void SetImageBytes(byte[] bytes)
    {
        ImageBytes = bytes;
        using var ms = new MemoryStream(bytes);
        var bitmap = new Bitmap(ms);
        this.RaiseAndSetIfChanged(ref _captchaImage, bitmap);
        this.RaisePropertyChanged(nameof(HasImage));
    }

    private Bitmap? _captchaImage;
    public Bitmap? CaptchaImage
    {
        get => _captchaImage;
        private set => this.RaiseAndSetIfChanged(ref _captchaImage, value);
    }

    public bool HasImage => _captchaImage != null;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ReactiveCommand<Unit, Unit> CopyImageCommand { get; }
    public ReactiveCommand<Unit, Unit> ConfirmCommand { get; }
    public event Action? CloseRequested;

    /// <summary>
    /// Set when user confirms — caller awaits this
    /// </summary>
    public TaskCompletionSource<CaptchaResult>? Tcs { get; set; }

    public ManualCaptchaViewModel()
    {
        this.WhenAnyValue(x => x.ErrorMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasError)));

        CopyImageCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (ImageBytes == null) return;
            // Copy base64-encoded image to clipboard
            // Avalonia 12: Use TopLevel storage clipboard API
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                var tl = desktop?.MainWindow;
                if (tl != null)
                {
                    var topLvl = Avalonia.Controls.TopLevel.GetTopLevel(tl);
                    if (topLvl?.Clipboard is { } cb)
                    {
                        var setText = cb.GetType().GetMethod("SetTextAsync");
                        if (setText != null)
                            await (Task)setText.Invoke(cb, [Convert.ToBase64String(ImageBytes)])!;
                    }
                }
            }
            catch { }
        });

        ConfirmCommand = ReactiveCommand.Create(() =>
        {
            var answer = CaptchaAnswer.Trim();
            if (string.IsNullOrWhiteSpace(answer))
            {
                ErrorMessage = "请输入验证码后再确认";
                return;
            }

            ErrorMessage = "";
            Tcs?.TrySetResult(new CaptchaResult
            {
                Expression = CaptchaExpression,
                Answer = answer,
                Success = true,
            });
            CloseRequested?.Invoke();
        });
    }
}
