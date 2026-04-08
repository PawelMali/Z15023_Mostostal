using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Z15023_Mostostal.PlcCommunication.Models;

namespace Z15023_Mostostal.State
{
    public class PlcDataStore
    {
        // Używamy ConcurrentDictionary dla pełnego bezpieczeństwa wątkowego
        // Klucz to ID sterownika PLC (1 do 4), Wartość to ostatnio odczytana struktura
        private readonly ConcurrentDictionary<int, SiemensDataRead> _latestPlcData = new();

        public void UpdateData(int plcId, SiemensDataRead data)
        {
            // Nadpisuje dane dla danego PLC lub dodaje nowe
            _latestPlcData[plcId] = data;
        }

        public SiemensDataRead? GetData(int plcId)
        {
            _latestPlcData.TryGetValue(plcId, out var data);
            return data; // Zwraca null, jeśli PLC jeszcze nic nie wysłał
        }
    }
}
