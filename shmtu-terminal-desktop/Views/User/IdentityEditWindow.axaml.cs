using Avalonia.Controls;
using shmtu.terminal.desktop.ViewModels.User;

namespace shmtu.terminal.desktop.Views.User;

public partial class IdentityEditWindow : Window
{
    public IdentityEditWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is IdentityEditViewModel vm)
            {
                vm.Saved += (_) => Close();
                vm.Cancelled += Close;
            }
        };
    }
}
