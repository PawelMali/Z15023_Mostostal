using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Z15023_Mostostal.PlcCommunication;
using Z15023_Mostostal.PlcCommunication.Drivers;
using Z15023_Mostostal.PlcCommunication.Models;

namespace Z15023_Mostostal.Tasks
{
    public class Task10_SendNewOrder
    {
        private readonly PlcDriverRegistry _driverRegistry;
        private readonly PlcChannelRegistry _channelRegistry;
        private readonly ILogger<Task10_SendNewOrder> _logger;

        public Task10_SendNewOrder(PlcDriverRegistry driverRegistry, PlcChannelRegistry channelRegistry, ILogger<Task10_SendNewOrder> logger)
        {
            _driverRegistry = driverRegistry;
            _channelRegistry = channelRegistry;
            _logger = logger;
        }

        public async Task<bool> ExecuteAsync(int plcId)
        {
            try
            {
                _logger.LogInformation("Rozpoczynam realizację Zadania 10 (Nowe Zlecenie) dla maszyny {PlcId}", plcId);

                // 1. Stworzenie generycznego zlecenia
                var newOrder = new SiemensOrderData
                {
                    Order_ID = 1001,
                    Product_Code = 55,
                    Target_Quantity = 200
                };

                // 2. Wysłanie danych do pamięci PLC (obszar "WriteOrder")
                var plcDriver = _driverRegistry.GetDriver(plcId);
                bool writeSuccess = await plcDriver.WriteAreaAsync("WriteOrder", newOrder);
                if (!writeSuccess)
                {
                    _logger.LogError("Nie udało się zapisać bloku zlecenia do PLC.");
                    return false;
                }

                // 3. Wysłanie Task 10 i OCZEKIWANIE na maszynę stanów (handshake)
                var channel = _channelRegistry.GetChannel(plcId);
                var taskRequest = new PlcTaskRequest(10); // Tworzymy żądanie zadania nr 10

                await channel.Writer.WriteAsync(taskRequest); // Wrzucamy do maszyny stanów

                _logger.LogInformation("Wystawiono rozkaz 10. Oczekiwanie na potwierdzenie od PLC...");

                // Magia TaskCompletionSource: Wątek zatrzymuje się tutaj i czeka,
                // aż pętla 100ms w PlcWorkerService wykona SetResult(true)!
                bool isConfirmedByPlc = await taskRequest.Tcs.Task;

                if (isConfirmedByPlc)
                {
                    _logger.LogInformation("Zadanie 10 zakończone pełnym sukcesem!");
                    return true;
                }

                return false;
            }
            catch (TimeoutException)
            {
                _logger.LogError("Timeout! PLC nie potwierdziło pobrania zlecenia 10 w określonym czasie.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas wysyłania zlecenia.");
                return false;
            }
        }
    }
}
