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
            // Trigger confirm command via DataContext
            // ViewModel will be created by the UI business coder
            if (DataContext is { } vm)
            {
                // Use reflection-free approach: find and click the confirm button
                // Or the ViewModel coder can bind this to a command
                var confirmMethod = vm.GetType().GetMethod("Confirm");
                confirmMethod?.Invoke(vm, null);
            }
        }
    }
}
