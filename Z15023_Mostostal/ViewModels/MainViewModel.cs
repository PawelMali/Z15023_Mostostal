using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Z15023_Mostostal.PlcCommunication;
using Z15023_Mostostal.State;
using Z15023_Mostostal.Tasks;

namespace Z15023_Mostostal.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PlcDataStore _dataStore;
        private readonly DispatcherTimer _uiRefreshTimer;

        private readonly PlcTaskManager _taskManager;

        // 1. Inicjalizujemy 4 modele dla naszych maszyn (Publiczne właściwości do bindowania)
        public PlcViewModel Plc1 { get; } = new() { PlcId = 1, MachineName = "Przygotowanie płaskownika" };
        public PlcViewModel Plc2 { get; } = new() { PlcId = 2, MachineName = "Montaż kraty" };
        public PlcViewModel Plc3 { get; } = new() { PlcId = 3, MachineName = "Zgrzewanie" };
        public PlcViewModel Plc4 { get; } = new() { PlcId = 4, MachineName = "Spawanie" };

        public MainViewModel(PlcDataStore dataStore, PlcTaskManager taskManager)
        {
            _dataStore = dataStore;
            _taskManager = taskManager;

            // Inicjalizacja timera odświeżającego UI (działa on na wątku UI)
            _uiRefreshTimer = new DispatcherTimer
            {
                // Odświeżamy ekrany co 300ms - to optymalne dla wzroku i wydajności
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _uiRefreshTimer.Tick += OnUiRefreshTick;
            _uiRefreshTimer.Start();
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

            if (plcData != null)
            {
                vm.IsConnected = true;
                vm.AutoMode = plcData.AutoMode;
                vm.ManualMode = plcData.ManualMode;
                vm.AutoActive = plcData.AutoActive;
                vm.Alarm = plcData.Alarm;
                vm.Warning = plcData.Warning;
                vm.PartCounter = plcData.PartCounter;
            }
            else
            {
                // Jeśli nie ma danych, uznajemy za rozłączony
                vm.IsConnected = false;
            }
        }

        [RelayCommand] // Ten atrybut automatycznie wygeneruje metodę ICommand dla przycisku w WPF
        private async Task SendOrderAsync()
        {
            // 1. Zdefiniowanie ID docelowego sterownika (może pochodzić z UI, np. ComboBox)
            int targetPlcId = 1;

            // 2. Wywołanie naszego dyspozytora. 
            // WĄTEK UI CZEKA TUTAJ (ale aplikacja nie "zamarza", można ją np. przesuwać po ekranie)
            bool success = await _taskManager.SendNewOrderAsync(targetPlcId);

            // 3. Reakcja na wynik zwrócony z TaskCompletionSource
            if (success)
            {
                MessageBox.Show($"Zlecenie wysłane do maszyny {targetPlcId} i potwerdzone przez sterownik PLC!",
                                "Sukces Handshake'u",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Błąd podczas wysyłania zlecenia do PLC {targetPlcId}. Sprawdź logi Serilog.",
                                "Błąd Komunikacji",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
