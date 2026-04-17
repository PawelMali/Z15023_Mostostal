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
using Z25023_Mostostal.ViewModels;

namespace Z25023_Mostostal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Konstruktor żąda MainViewModel, który kontener DI automatycznie tu podstawi
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            // Magia MVVM: Przypisujemy ViewModel do DataContextu okna
            DataContext = viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Wymusza na całym środowisku WPF rozpoczęcie procedury zamknięcia
            Application.Current.Shutdown();
        }

        private void serviceBn_Click(object sender, RoutedEventArgs e)
        {
            //if (serviceStackPanel.Visibility == Visibility.Visible)
            //    serviceStackPanel.Visibility = Visibility.Hidden;
            //else
            //    serviceStackPanel.Visibility = Visibility.Visible;
        }

    }
}