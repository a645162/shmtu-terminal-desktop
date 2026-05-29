using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace shmtu.terminal.desktop.Views.Component.Captcha;

public partial class ManualCaptchaWindow : Window
{
    public ManualCaptchaWindow()
    {
        InitializeComponent();

        // Auto-focus answer box
        Opened += (_, _) =>
        {
            if (this.FindControl<TextBox>("AnswerBox") is { } box)
                box.Focus();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ViewModels.Component.Captcha.ManualCaptchaViewModel vm)
        {
            vm.ConfirmCommand.Execute().Subscribe();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
