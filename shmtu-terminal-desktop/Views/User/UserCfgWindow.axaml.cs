using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace shmtu.terminal.desktop.Views.User;

public partial class UserCfgWindow : Window
{
    public UserCfgWindow()
    {
        InitializeComponent();
    }

    private void Button_User_Add_OnClick(object? sender, RoutedEventArgs e)
    {
        ListBoxUserList.ItemsSource = new List<string>
        {
            "User 1",
            "User 2",
            "User 3"
        };
    }

    private void Button_Save_OnClick(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("ListBoxUserList.Items.Count");
        Console.WriteLine(ListBoxUserList.Items.Count);
    }
}