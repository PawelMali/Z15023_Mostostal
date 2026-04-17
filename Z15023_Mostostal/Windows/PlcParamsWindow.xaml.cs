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

namespace Z25023_Mostostal.Windows
{
    /// <summary>
    /// Interaction logic for PlcParamsWindow.xaml
    /// </summary>
    public partial class PlcParamsWindow : Window
    {
        public PlcParamsWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Ta metoda po prostu zamyka aktualne okno
            this.Close();
        }
    }
}
