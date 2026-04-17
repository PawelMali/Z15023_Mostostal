using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Z25023_Mostostal.PlcCommunication.Models;

namespace Z25023_Mostostal.State
{
    public class PlcDataStore
    {
        // Używamy ConcurrentDictionary dla pełnego bezpieczeństwa wątkowego
        // Klucz to ID sterownika PLC (1 do 4), Wartość to ostatnio odczytana struktura

        // Słownik dla danych maszynowych (AutoMode, Life, Liczniki)
        private readonly ConcurrentDictionary<int, SiemensReadData> _latestPlcData = new();

        // Słownik dla aktualnie produkowanego zlecenia
        private readonly ConcurrentDictionary<int, SiemensOrderData> _currentOrders = new();

        // Słownik dla aktualnej konfiguracji maszyny (np. parametry z ReadConfig)
        private readonly ConcurrentDictionary<int, SiemensConfigData> _currentConfigs = new();

        // NOWE: Kolejka logów dla każdej maszyny(bezpieczna wątkowo)
        private readonly ConcurrentDictionary<int, ConcurrentQueue<string>> _taskLogs = new();


        // Zdarzenie odpalane w momencie dodania logu (Przekazuje PlcId i Treść)
        public event Action<int, string>? OnLogAdded;

        // Nadpisuje dane dla danego PLC lub dodaje nowe
        public void UpdateData(int plcId, SiemensReadData data) => _latestPlcData[plcId] = data;

        public void UpdateCurrentOrder(int plcId, SiemensOrderData orderData) => _currentOrders[plcId] = orderData;

        public void UpdateCurrentConfig(int plcId, SiemensConfigData config) => _currentConfigs[plcId] = config;

        public SiemensConfigData? GetCurrentConfig(int plcId) => _currentConfigs.TryGetValue(plcId, out var data) ? data : null;

        public SiemensReadData? GetData(int plcId)
        {
            _latestPlcData.TryGetValue(plcId, out var data);
            return data; // Zwraca null, jeśli PLC jeszcze nic nie wysłał
        }

        public SiemensOrderData? GetCurrentOrder(int plcId)
        {
            _currentOrders.TryGetValue(plcId, out var data);
            return data; // Zwraca null, jeśli PLC jeszcze nic nie wysłał
        }

        public void ClearData(int plcId)
        {
            // Usuwamy dane, co dla UI oznacza: "Rozłączono"
            _latestPlcData.TryRemove(plcId, out _);
            _currentOrders.TryRemove(plcId, out _);
        }

        /// <summary>
        /// Dodaje nowy log zdarzenia. Wywoływane z wątków w tle (Worker / Router).
        /// </summary>
        public void AddTaskLog(int plcId, string message)
        {
            var queue = _taskLogs.GetOrAdd(plcId, _ => new ConcurrentQueue<string>());

            string time = DateTime.Now.ToString("HH:mm:ss");
            string formattedMsg = $"[{time}] {message}";

            queue.Enqueue(formattedMsg);

            // Utrzymujemy tylko 15 ostatnich elementów, żeby nie zapchać pamięci
            while (queue.Count > 15)
            {
                queue.TryDequeue(out _);
            }

            // Informujemy UI, że dodano nowy log
            OnLogAdded?.Invoke(plcId, formattedMsg);
        }

        /// <summary>
        /// Pobiera całą historię (np. przy pierwszym otwarciu okna).
        /// </summary>
        public IEnumerable<string> GetRecentLogs(int plcId)
        {
            return _taskLogs.TryGetValue(plcId, out var queue)
                ? queue.ToArray().Reverse() // Reverse, żeby najnowsze były na górze
                : Enumerable.Empty<string>();
        }
    }
}
