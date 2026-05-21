using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Z25023_Mostostal.PlcCommunication.Models;

namespace Z25023_Mostostal.Windows;

public partial class CuttingDataWindow : Window
{
    // Kolekcja dla naszych stempli, obserwowalna przez interfejs WPF
    private ObservableCollection<PunchVisualItem> _evenPunches = new();
    private ObservableCollection<PunchVisualItem> _oddPunches = new();

    // Płaska tablica referencji do szybkiego aktualizowania maski bitowej po indeksach (0-31)
    private PunchVisualItem[] _allPunches = new PunchVisualItem[32];

    public CuttingDataWindow(SiemensCuttingData data)
    {
        InitializeComponent();

        // 1. Inicjalizacja i sortowanie stempli do odpowiednich rzędów
        for (int i = 0; i < 32; i++)
        {
            var punch = new PunchVisualItem { Index = i, IsActive = false };
            _allPunches[i] = punch; // Zapisujemy referencję do płaskiej tablicy

            if (i % 2 == 0)
                _evenPunches.Add(punch); // Parzyste -> Górny rząd
            else
                _oddPunches.Add(punch);  // Nieparzyste -> Dolny rząd
        }

        // Podpinamy kolekcje pod widok
        EvenPunchesVisualizer.ItemsSource = _evenPunches;
        OddPunchesVisualizer.ItemsSource = _oddPunches;

        // Zabezpieczenie: Odfiltrowujemy puste indeksy (Lp > 0)
        // Oraz MAPUJEMY Pola (Fields) na Właściwości (Properties) anonimowego obiektu
        CuttingGrid.ItemsSource = data.steps
            .Where(s => s.Lp > 0)
            .Select(s => new
            {
                Lp = s.Lp,
                Delta = s.Delta,
                CutPosition = s.CutPosition,
                Punch = s.Punch,
                Cut = s.Cut
            })
            .ToList();
    }

    private void CuttingGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CuttingGrid.SelectedItem == null) return;

        dynamic selectedRow = CuttingGrid.SelectedItem;
        int mask = selectedRow.Punch;

        // Aktualizacja odbywa się na płaskiej tablicy
        for (int i = 0; i < 32; i++)
        {
            bool isActive = (mask & (1 << i)) != 0;
            _allPunches[i].IsActive = isActive;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}


// Model reprezentujący jeden graficzny stempel. 
// Zapewnia odświeżenie UI po zmianie koloru dzięki INotifyPropertyChanged
public class PunchVisualItem : INotifyPropertyChanged
{
    private bool _isActive;

    public int Index { get; set; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                // Informujemy widok WPF, że zmienił się stan i należy pobrać nowe kolory
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextBrush)));
            }
        }
    }

    // Dynamicznie generowane kolory tła i tekstu
    public SolidColorBrush BackgroundBrush => IsActive ? Brushes.Firebrick : Brushes.WhiteSmoke;
    public SolidColorBrush TextBrush => IsActive ? Brushes.White : Brushes.Gray;

    public event PropertyChangedEventHandler? PropertyChanged;
}