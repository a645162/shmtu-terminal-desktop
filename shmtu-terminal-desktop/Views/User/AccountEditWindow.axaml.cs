using Avalonia.Controls;
using shmtu.terminal.desktop.ViewModels.User;

namespace shmtu.terminal.desktop.Views.User;

public partial class AccountEditWindow : Window
{
    public AccountEditWindow()
    {
        InitializeComponent();

        // Auto-close when ViewModel requests it
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AccountEditViewModel vm)
            {
                vm.Saved += Close;
                vm.Cancelled += Close;
            }
        };
    }
}
