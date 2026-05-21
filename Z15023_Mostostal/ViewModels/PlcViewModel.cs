using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.State;

namespace Z25023_Mostostal.ViewModels
{
    // Reprezentuje stan pojedynczego sterownika (Bindowane do UserControl)
    public partial class PlcViewModel : ObservableObject
    {
        [ObservableProperty] private int _plcId;
        [ObservableProperty] private string _machineName = string.Empty;

        // Zmienne procesowe z ASComm
        [ObservableProperty] private short _lifeSignal;
        [ObservableProperty] private bool _autoMode;
        [ObservableProperty] private bool _manualMode;
        [ObservableProperty] private bool _autoActive;
        [ObservableProperty] private bool _alarm;
        [ObservableProperty] private bool _warning;
        [ObservableProperty] private short _partCounter;

        [ObservableProperty] private string _currentOrderNumber = "BRAK";
        [ObservableProperty] private string _currentOrderType = "-";
        [ObservableProperty] private string _currentOrderDimensions = "- x -";
        [ObservableProperty] private string _currentOrderProgress = "0 / 0";

        private readonly DispatcherTimer _refreshTimer;

        // Dodatkowy status (np. jeśli PLC przestanie odpowiadać)
        [ObservableProperty] private bool _isConnected = true;

        public ObservableCollection<string> EventLogs { get; } = new();
        private readonly PlcDataStore _dataStore;

        private readonly Action<int> _openOrderDetailsAction;
        private readonly Action<int> _openPlcParamsAction;
        private readonly Action<int> _openCuttingDataAction;

        public PlcViewModel(int plcId, PlcDataStore dataStore, Action<int> openOrderDetails, Action<int> openPlcParams, Action<int> openCuttingData)
        {
            _plcId = plcId;
            _dataStore = dataStore;
            _openOrderDetailsAction = openOrderDetails;
            _openPlcParamsAction = openPlcParams;
            _openCuttingDataAction = openCuttingData;

            // Wczytujemy to, co już było w historii logów dla tego PLC 
            foreach (var log in _dataStore.GetRecentLogs(plcId))
            {
                EventLogs.Add(log);
            }

            // Podpinamy się pod nasłuchiwanie nowych logów
            _dataStore.OnLogAdded += DataStore_OnLogAdded;
        }

        // --- KOMENDY PRZYCISKÓW ---
        [RelayCommand]
        private void ShowOrderDetails() => _openOrderDetailsAction?.Invoke(PlcId);

        [RelayCommand]
        private void ShowPlcParams() => _openPlcParamsAction?.Invoke(PlcId);

        [RelayCommand]
        private void ShowCuttingData() => _openCuttingDataAction?.Invoke(PlcId);

        [RelayCommand]
        private void ClearLog()
        {     EventLogs.Clear();
        }


        // --- METODA AKTUALIZUJĄCA DANE (Wywoływana np. z MainViewModel co 500ms) ---
        public void RefreshData(SiemensReadData plcData, SiemensOrderData currentOrder)
        {
            // ... Aktualizacja starych danych (IsConnected, AutoMode) ...

            // Aktualizacja Zlecenia
            if (currentOrder != null && !string.IsNullOrWhiteSpace(currentOrder.KOLZLEC.ToString()))
            {
                CurrentOrderNumber = currentOrder.KOLZLEC.ToString();
                CurrentOrderType = currentOrder.TYP.ToString();
                CurrentOrderDimensions = $"{currentOrder.DLUGOSC} x {currentOrder.SZEROKOSC}";

                // Formatowanie np. "15 / 50" (zrobione / zadane)
                // Założyłem, że PartCounter z PLC to zrobione sztuki, a SZTUKPOZ to zadane
                CurrentOrderProgress = $"{plcData.PartCounter} / {currentOrder.SZTUKPOZ}";
            }
            else
            {
                CurrentOrderNumber = "OCZEKIWANIE...";
                CurrentOrderType = "-";
                CurrentOrderDimensions = "-";
                CurrentOrderProgress = "-";
            }
        }

        private void DataStore_OnLogAdded(int incomingPlcId, string message)
        {
            // Jeśli log dotyczy innej maszyny - ignorujemy
            if (incomingPlcId != PlcId) return;

            // Bezpieczne przekazanie zadania do wątku UI (WPF Dispatcher)
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                EventLogs.Insert(0, message); // Dodajemy na samą górę ListBoxa

                // Usuwamy nadmiar z dołu widoku
                if (EventLogs.Count > 15)
                {
                    EventLogs.RemoveAt(EventLogs.Count - 1);
                }
            });
        }
    }
}
