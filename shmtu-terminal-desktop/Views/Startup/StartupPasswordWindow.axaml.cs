using Avalonia.Controls;
using Avalonia.Input;

namespace shmtu.terminal.desktop.Views.Startup;

public partial class StartupPasswordWindow : Window
{
    public StartupPasswordWindow()
    {
        InitializeComponent();
    }

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // HIGH 17 fix: 直接调用 ConfirmCommand，避免反射
            if (DataContext is ViewModels.Startup.StartupPasswordViewModel vm)
            {
                vm.ConfirmCommand.Execute().Subscribe();
            }
        }
    }
}
