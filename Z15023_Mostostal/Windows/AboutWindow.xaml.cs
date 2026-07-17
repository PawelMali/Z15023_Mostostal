using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Z25023_Mostostal.Windows
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            var fileVersion = FileVersionInfo
                .GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                .FileVersion;

            VersionText.Text = FormatVersion(fileVersion);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Zamyka okno modalne
            this.Close();
        }

        public static string FormatVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return version;

            var parts = version.Split('.');

            if (parts.Length < 4)
                return version;

            // część główna (1.2.0)
            string main = $"{parts[0]}.{parts[1]}.{parts[2]}";

            // parsowanie daty
            string datePart = parts[3];

            if (datePart.Length != 12)
                return main;

            string formattedDate =
                $"{datePart.Substring(0, 4)}." +   // rok
                $"{datePart.Substring(4, 2)}." +   // miesiąc
                $"{datePart.Substring(6, 2)} " +   // dzień
                $"{datePart.Substring(8, 2)}:" +   // godzina
                $"{datePart.Substring(10, 2)}";    // minuta

            return $"{main} ({formattedDate})";
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }

    }
}
