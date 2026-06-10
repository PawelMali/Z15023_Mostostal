using AutomatedSolutions.ASCommStd.SI.S7;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Z25023_Mostostal.PlcCommunication.Models;
using SIS7 = AutomatedSolutions.ASCommStd.SI.S7;

namespace Z25023_Mostostal.PlcCommunication.Drivers
{
    public class AsCommPlcDriver : IPlcDriver
    {
        private readonly ILogger<AsCommPlcDriver> _logger;
        private readonly PlcConnectionConfig _config;

        public string PlcName => _config.Name; 

        public bool HasConfigurationError { get; private set; } = false;
        private bool _isInErrorState = false;
        private bool _isInitialized = false;

        private int _consecutiveErrors = 0;

        // Obiekty ASComm (Tworzone raz)
        private SIS7.Net.Channel? _channel;
        private SIS7.Device? _device;
        private SIS7.Group? _group;


        // Słownik przechowujący gotowe obiekty Item pod logicznymi nazwami
        private readonly Dictionary<string, SIS7.Item> _items = new();

        private volatile bool _isConnected;
        public bool IsConnected => _isConnected;

        public AsCommPlcDriver(ILogger<AsCommPlcDriver> logger, PlcConnectionConfig config)
        {
            _logger = logger;
            _config = config;

            InitializeAsComm();
        }

        /// <summary>
        /// Buduje strukturę biblioteki ASComm tylko raz w cyklu życia obiektu.
        /// </summary>
        private void InitializeAsComm()
        {
            try
            {
                _channel = new SIS7.Net.Channel();
                _device = new SIS7.Device($"{_config.IpAddress},{_config.Rack},{_config.Slot}", SIS7.Model.S7_1500, 1000, 100) { Link = SIS7.LinkType.PC };
                _group = new SIS7.Group(false, 50);

                _channel.Devices.Add(_device);
                _device.Groups.Add(_group);

                _channel.Error += (s, e) => HandleAsyncError("Channel", e.Message);
                _device.Error += (s, e) => HandleAsyncError("Device", e.Message);

                // =========================================================
                // RĘCZNA, BEZPIECZNA REJESTRACJA WSZYSTKICH OBSZARÓW
                // Wykorzystujemy GetStructureLength() z Twoich klas UDT
                // =========================================================

                // 1. Obszar: ReadData
                var structRead = new SiemensReadData();
                RegisterItem("ReadData", _config.MemoryMap.ReadData, structRead.GetStructureLength());

                // 2. Obszar: WriteData
                var structWrite = new SiemensWriteData();
                RegisterItem("WriteData", _config.MemoryMap.WriteData, structWrite.GetStructureLength());

                // 3. Obszar: ReadOrder / WriteOrder
                var structOrder = new SiemensOrderData();
                RegisterItem("ReadOrder", _config.MemoryMap.ReadOrder, structOrder.GetStructureLength());
                RegisterItem("WriteOrder", _config.MemoryMap.WriteOrder, structOrder.GetStructureLength());

                // 4. Obszar: ReadConfig / WriteConfig
                var structConfig = new SiemensConfigData();
                RegisterItem("ReadConfig", _config.MemoryMap.ReadConfig, structConfig.GetStructureLength());
                RegisterItem("WriteConfig", _config.MemoryMap.WriteConfig, structConfig.GetStructureLength());

                // 5. Obszar: WriteCuttingData (Pętla zgeneralizowana dla prasy i noża)
                var structCutting = new SiemensCuttingData();
                RegisterItem("WriteCuttingData", _config.MemoryMap.WriteCuttingData, structCutting.GetStructureLength());
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Błąd inicjalizacji ASComm dla IP: {Ip}", _config.IpAddress);
                throw;
            }
        }

        /// <summary>
        /// Metoda pomocnicza do tworzenia i przypinania Itemów
        /// </summary>
        private void RegisterItem(string logicalName, string addressDb, int structureLength)
        {
            // Jeśli w konfiguracji brakuje adresu, pomijamy
            if (string.IsNullOrWhiteSpace(addressDb)) return;

            // Oczekujemy, że adresy w konfiguracji to np. "DB10", dlatego dodajemy .DBB0
            string fullAddress = addressDb.Contains(".DBB") ? addressDb : $"{addressDb}.DBB0";

            var item = new SIS7.Item
            {
                Label = logicalName,
                HWTagName = fullAddress,
                Elements = 1,
                HWDataType = SIS7.DataType.Structure,
                StructureLength = structureLength
            };

            _group!.Items.Add(item);
            _items.Add(logicalName, item);
        }

        public async Task<bool> ConnectAsync()
        {
            // LENIWA INICJALIZACJA WYKONYWANA W TLE
            //if (!_isInitialized)
            //{
            //    await Task.Run(() => InitializeAsComm());
            //}

            if (_isConnected) return true;

            try
            {
                // Wymuszenie testowego odczytu bloku wejściowego (z Twojego oryginału)
                //await Task.Run(() =>
              //  {
                    if (_items.TryGetValue("ReadData", out var testItem))
                    {
                        testItem.Read();
                    }
               // });

                _isConnected = true;
                HasConfigurationError = false;
                _consecutiveErrors = 0;

                if (_isInErrorState)
                {
                    _logger.LogInformation("Przywrócono połączenie z PLC {Ip}!", _config.IpAddress);
                    _isInErrorState = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                // Sprawdzamy, czy to błąd brakującego obiektu/DB
                if (ex.Message.Contains("Object does not exist") || ex.Message.Contains("0x0000000a")
                    || ex.Message.Contains("Address does not exist") || ex.Message.Contains("0x00000005"))
                {
                    if (_consecutiveErrors <= 3)
                    {
                        _logger.LogCritical("BŁĄD KONFIGURACJI ({Count}/3): Pamięć na PLC {Ip} nie istnieje! ({Msg}). Zatrzymano połączenie.", _consecutiveErrors, _config.IpAddress, ex.Message);
                    }
                    HasConfigurationError = true;
                }
                else
                {
                    // Logujemy tylko, jeśli to pierwszy błąd z serii
                    if (_consecutiveErrors <= 3)
                    {
                        _logger.LogError("Błąd połączenia ({Count}/3) z PLC {Ip}: {Msg}", _consecutiveErrors, _config.IpAddress, ex.Message);
                    
                        _isInErrorState = true;
                    }
                }

                _isConnected = false;
                return false;
            }
        }

        public async Task<T?> ReadAreaAsync<T>(string areaName) where T : class, new()
        {
            if (!_isConnected) return null;

            if (!_items.TryGetValue(areaName, out var item))
            {
                _logger.LogError("Próba odczytu niezarejestrowanego obszaru: {Area}", areaName);
                return null;
            }

            try
            {
                T resultData = new T();
                //await Task.Run(() =>
                //{
                    item.Read();
                    item.GetStructuredValues(resultData);
                //});
                return resultData;
            }
            catch (Exception ex)
            {
                {
                    // Sprawdzamy czy dany blok danych (np. DB dla ReadOrder) istnieje na sterowniku
                    if (ex.Message.Contains("Object does not exist") || ex.Message.Contains("0x0000000a")
                        || ex.Message.Contains("Address does not exist") || ex.Message.Contains("0x00000005")
                        || ex.Message.Contains("beyond the CPU's address range"))
                    {
                        _logger.LogCritical("KRYTYCZNY BŁĄD KONFIGURACJI: Obszar '{Area}' na PLC {Ip} jest poza zakresem lub nie istnieje! {Msg}", areaName, _config.IpAddress, ex.Message);
                        HasConfigurationError = true;
                        _isConnected = false;
                        return null;
                    }

                    _isConnected = false;
                    _consecutiveErrors++;

                    if (_consecutiveErrors <= 3)
                    {
                        _logger.LogWarning("Utracono komunikację przy odczycie ({Count}/3) {Area} ({Ip}): {Msg}", _consecutiveErrors, areaName, _config.IpAddress, ex.Message);
                    }
                    _isInErrorState = true;
                    return null;
                }
            }
        }

        public async Task<bool> WriteAreaAsync<T>(string areaName, T data) where T : class
        {
            if (!_isConnected) return false;

            if (!_items.TryGetValue(areaName, out var item))
            {
                if (_consecutiveErrors <= 3)
                {
                    _logger.LogError("Próba zapisu do niezarejestrowanego obszaru: {Area}", areaName);
                }
                return false;
            }

            try
            {
                //await Task.Run(() =>
                //{
                    item.Write(data);
                //});
                return true;
            }
            catch (Exception ex)
            {
                // Sprawdzamy błąd zakresu/obecności DB również przy zapisie
                if (ex.Message.Contains("Object does not exist") || ex.Message.Contains("0x0000000a")
                    || ex.Message.Contains("Address does not exist") || ex.Message.Contains("0x00000005")
                    || ex.Message.Contains("beyond the CPU's address range"))
                {
                    _logger.LogCritical("KRYTYCZNY BŁĄD KONFIGURACJI: Zapis do obszaru '{Area}' na PLC {Ip} niemożliwy - brak bloku/zakresu! {Msg}", areaName, _config.IpAddress, ex.Message);
                    HasConfigurationError = true;
                    _isConnected = false;
                    return false;
                }

                _isConnected = false;
                _consecutiveErrors++;

                if (_consecutiveErrors <= 3)
                {
                    _logger.LogWarning("Utracono komunikację przy zapisie ({Count}/3) {Area} ({Ip}): {Msg}", _consecutiveErrors, areaName, _config.IpAddress, ex.Message);
                }
                _isInErrorState = true;
                return false;
            }
        }

        private void HandleAsyncError(string source, string message)
        {
            _isConnected = false;
            if (_consecutiveErrors <= 3)
            {
                _logger.LogError("Błąd ASComm [{Source}] ({Count}/3) dla {Ip}: {Msg}", source, _consecutiveErrors, _config.IpAddress, message);
            }
            _isInErrorState = true;
            
        }

        public void Dispose()
        {
            try
            {
                _logger.LogInformation("Zamykanie zasobów sterownika ASComm dla {Ip}", _config.IpAddress);
                if (_channel != null)
                {
                    // Czyste usunięcie struktury wg zaleceń producenta
                    _channel.Devices.Clear();
                    _channel.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania sterownika ASComm");
            }

            GC.SuppressFinalize(this);
        }
    }
}
