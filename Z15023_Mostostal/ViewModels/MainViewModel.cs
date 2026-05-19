using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services;
using Z25023_Mostostal.Services.RecipeManager;
using Z25023_Mostostal.Settings;
using Z25023_Mostostal.State;
using Z25023_Mostostal.Tasks.Outbound_PC_PLC;
using Z25023_Mostostal.Windows;

namespace Z25023_Mostostal.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PlcDataStore _dataStore;
        private readonly ParameterDefinitionService _parameterDefinitionService;
        private readonly DispatcherTimer _uiRefreshTimer;

        private readonly OutboundTaskDispatcher _outboundDispatcher;

        private readonly IServiceProvider _serviceProvider;
        private readonly OrderRepository _orderRepository;

        [ObservableProperty] private ObservableCollection<ProductionOrder> _orders = new();
        [ObservableProperty] private ProductionOrder? _selectedOrder;

        // 1. Inicjalizujemy 4 modele dla naszych maszyn (Publiczne właściwości do bindowania)
        public PlcViewModel Plc1 { get; }
        public PlcViewModel Plc2 { get; }
        public PlcViewModel Plc3 { get; }
        public PlcViewModel Plc4 { get; }

        public MainViewModel(List<PlcConnectionConfig> configs, PlcDataStore dataStore, ParameterDefinitionService parameterDefinitionService, OutboundTaskDispatcher taskManager, IServiceProvider serviceProvider, OrderRepository orderRepository)
        {
            _dataStore = dataStore;
            _parameterDefinitionService = parameterDefinitionService;
            _outboundDispatcher = taskManager;
            _serviceProvider = serviceProvider;
            _orderRepository = orderRepository;

            // Inicjalizacja timera odświeżającego UI (działa on na wątku UI)
            _uiRefreshTimer = new DispatcherTimer
            {
                // Odświeżamy ekrany co 300ms - to optymalne dla wzroku i wydajności
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _uiRefreshTimer.Tick += OnUiRefreshTick;
            _uiRefreshTimer.Start();

            var name1 = configs.FirstOrDefault(c => c.Id == 1)?.Name ?? "Sterownik 1";
            var name2 = configs.FirstOrDefault(c => c.Id == 2)?.Name ?? "Sterownik 2";
            var name3 = configs.FirstOrDefault(c => c.Id == 3)?.Name ?? "Sterownik 3";
            var name4 = configs.FirstOrDefault(c => c.Id == 4)?.Name ?? "Sterownik 4";

            Plc1 = new PlcViewModel(1, dataStore, OpenOrderDetails, OpenPlcParams, OpenCuttingData) { MachineName = name1 };
            Plc2 = new PlcViewModel(2, dataStore, OpenOrderDetails, OpenPlcParams, OpenCuttingData) { MachineName = name2 };
            Plc3 = new PlcViewModel(3, dataStore, OpenOrderDetails, OpenPlcParams, OpenCuttingData) { MachineName = name3 };
            Plc4 = new PlcViewModel(4, dataStore, OpenOrderDetails, OpenPlcParams, OpenCuttingData) { MachineName = name4 };
        }

        private void OnUiRefreshTick(object? sender, EventArgs e)
        {
            // Aktualizujemy każdy model w pętli UI
            UpdatePlcState(Plc1);
            UpdatePlcState(Plc2);
            UpdatePlcState(Plc3);
            UpdatePlcState(Plc4);
        }

        private void UpdatePlcState(PlcViewModel vm)
        {
            // Pobieramy najświeższy zrzut struktury SiemensDataRead dla danego ID
            var plcData = _dataStore.GetData(vm.PlcId);
            var plcOrder = _dataStore.GetCurrentOrder(vm.PlcId);

            if (plcData != null && plcOrder != null)
            {
                vm.IsConnected = true;
                vm.AutoMode = plcData.AutoMode;
                vm.ManualMode = plcData.ManualMode;
                vm.AutoActive = plcData.AutoActive;
                vm.Alarm = plcData.Alarm;
                vm.Warning = plcData.Warning;
                vm.PartCounter = plcData.PartCounter;
                vm.LifeSignal = plcData.Life;

                

                vm.CurrentOrderNumber = plcOrder.KOLZLEC.ToString();
                vm.CurrentOrderType = plcOrder.TYP.ToString();
                vm.CurrentOrderDimensions = $"{plcOrder.DLUGOSC} x {plcOrder.SZEROKOSC}";

                // Formatowanie np. "15 / 50" (zrobione / zadane)
                // Założyłem, że PartCounter z PLC to zrobione sztuki, a SZTUKPOZ to zadane
                vm.CurrentOrderProgress = $"{plcData.PartCounter} / {plcOrder.SZTUKPOZ}";
            }
            else
            {
                // Jeśli nie ma danych, uznajemy za rozłączony
                vm.IsConnected = false;
            }
        }

        private async void OpenCuttingData(int plcId)
        {
            // Pobieramy instancję fizycznego zarejestrowanego drivera dla wybranego PLC
            var driverRegistry = _serviceProvider.GetRequiredService<PlcDriverRegistry>();
            var driver = driverRegistry.GetDriver(plcId);

            if (driver == null || !driver.IsConnected)
            {
                MessageBox.Show("Brak aktywnego połączenia ze sterownikiem maszyny.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pobieramy aktualne przeliczone dane z bloku DB na żądanie
            var cuttingData = await driver.ReadAreaAsync<SiemensCuttingData>("WriteCuttingData");

            if (cuttingData == null)
            {
                MessageBox.Show("Nie udało się odczytać profilu cięcia z pamięci PLC lub blok danych jest pusty.", "Błąd komunikacji", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Wyświetlenie okna modalnego nad oknem głównym
            var window = new Z25023_Mostostal.Windows.CuttingDataWindow(cuttingData);
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }


        [RelayCommand]
        public async Task RefreshOrdersAsync()
        {
            var data = await _orderRepository.GetPendingOrdersAsync();
            Orders = new ObservableCollection<ProductionOrder>(data);
        }

        [RelayCommand]
        public async Task SendSelectedOrderToPlcAsync()
        {
            if (SelectedOrder == null) return;

            // Zakładamy, że wysyłamy do PLC o ID 1 (można to zmienić na wybór z UI)
            int targetPlcId = 1;

            // Wywołujemy Task 10 przez nasz OutboundDispatcher
            bool success = await _outboundDispatcher.SendTask10_NewOrderAsync(targetPlcId, SelectedOrder);

            if (success)
                MessageBox.Show("Zlecenie wysłane pomyślnie!");
        }

        [RelayCommand]
        private void OpenSettings()
        {
            // Pobieramy nowe okno z kontenera DI
            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();

            // Otwieramy okno (zablokuje główne okno do czasu zamknięcia)
            settingsWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenSimulation()
        {
            // Pobieramy zlecenia dla maszyny 1
            var orderData = _dataStore.GetCurrentOrder(1);

            // Jeśli nie ma danych o zleceniu lub PLC jest rozłączone
            if (orderData == null || string.IsNullOrWhiteSpace(orderData.KOLZLEC.ToString()))
            {
                // Zmieniamy MessageBox na pytanie (Yes/No)
                var result = MessageBox.Show(
                    "Brak aktualnego zlecenia na PLC 1 lub maszyna jest rozłączona.\n\nCzy chcesz otworzyć symulator z domyślnymi parametrami, aby móc uzupełnić dane ręcznie?",
                    "Brak komunikacji z PLC Cięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                // Jeśli użytkownik wybierze "Tak"
                if (result == MessageBoxResult.Yes)
                {
                    // Wywołujemy standardowy konstruktor (bez parametrów z PLC)
                    // Okno załaduje domyślne dane z XAML i poczeka na kliknięcie przycisku przez operatora
                    var defaultSimWindow = new Z25023_Mostostal_Cięcie.MainWindow();
                    defaultSimWindow.Owner = Application.Current.MainWindow;
                    defaultSimWindow.ShowDialog();
                }

                // Niezależnie od wyboru wychodzimy z metody, żeby nie parsować pustych danych poniżej
                return;
            }

            // Mapowanie zgodnie z wytycznymi
            double length = Math.Round(orderData.DRZECZ_BBL, 2);
            double pitch = Math.Round(orderData.OCZKOL_CBS, 2);
            double marginLeft = Math.Round(orderData.PSKRAJDL_TEF, 2);
            double marginRight = Math.Round(orderData.PSKRAJDL2_DEF, 2);

            // Sprawdzamy czy w typie znajduje się litera "S" (lub "s"). 
            // StringComparison.OrdinalIgnoreCase zapewnia poprawne działanie niezależnie od wielkości liter.
            string orderType = orderData.TYP.ToString();
            bool isSerration = orderType.Contains("S", StringComparison.OrdinalIgnoreCase);

            // Wywołujemy okno z projektu Z25023_Mostostal_Cięcie
            var simWindow = new Z25023_Mostostal_Cięcie.MainWindow(
                length, pitch, marginLeft, marginRight, isSerration);

            // Ustawiamy okno główne jako właściciela, by ładnie wyświetliło się na środku
            simWindow.Owner = Application.Current.MainWindow;

            // Używamy ShowDialog() jeśli chcemy zablokować apkę pod spodem, 
            // lub Show(), jeśli symulator ma działać jako niezależne okno (zakładam Show).
            simWindow.ShowDialog();
        }

        private void OpenOrderDetails(int plcId)
        {
            // Pobieramy dane z naszego magazynu stanów
            var orderData = _dataStore.GetCurrentOrder(plcId);

            if (orderData == null)
            {
                MessageBox.Show("Brak danych o zleceniu dla tego PLC.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Tworzymy okno i przekazujemy mu dane
            var vm = new OrderDetailsViewModel(orderData);
            var window = new OrderDetailsWindow { DataContext = vm };

            // Ustawienie okna głównego jako właściciela (dla wyśrodkowania)
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }

        private void OpenPlcParams(int plcId)
        {
            var configData = _dataStore.GetCurrentConfig(plcId);

            if (configData == null)
            {
                MessageBox.Show("Brak danych konfiguracyjnych dla tego PLC.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // _parameterDefinitionService musisz wstrzyknąć do konstruktora MainViewModelu
            var vm = new PlcParamsViewModel(plcId, configData, _parameterDefinitionService);
            var window = new PlcParamsWindow { DataContext = vm };

            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }
    }
}
