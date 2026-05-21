using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Z25023_Mostostal.ViewModels;

namespace Z25023_Mostostal;

/// <summary>
/// Logika interakcji dla klasy SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Podpinamy akcję zamykania okna pod nasz ViewModel
        _viewModel.CloseWindowAction = this.Close;
    }

    private void DbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Gdy użytkownik wpisuje hasło, przekazujemy je do zmiennej w ViewModelu
        if (DataContext is SettingsViewModel vm)
        {
            vm.NewPassword = DbPasswordBox.Password;
        }
    }

    private void LocalDbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Gdy użytkownik wpisuje hasło, przekazujemy je do zmiennej w ViewModelu
        if (DataContext is SettingsViewModel vm)
        {
            vm.LocalNewPassword = LocalDbPasswordBox.Password;
        }
    }

}
