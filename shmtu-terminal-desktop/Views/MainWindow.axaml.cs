using Avalonia.Controls;
using Avalonia.Interactivity;

namespace shmtu.terminal.desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Check Is Debug Mode
        var isDebugMode = System.Diagnostics.Debugger.IsAttached;
        if (isDebugMode)
        {
            Title += " - Debug Mode";
        }
    }
}
