using Microsoft.Extensions.Logging;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Drivers;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services.RecipeManager;

namespace Z25023_Mostostal.Tasks.Outbound_PC_PLC
{
    public class Task10_SendNewOrder
    {
        private readonly PlcDriverRegistry _driverRegistry;
        private readonly PlcChannelRegistry _channelRegistry;
        private readonly ProductHashGenerator _hashGenerator;
        private readonly RecipeRepository _recipeRepo;
        private readonly ILogger<Task10_SendNewOrder> _logger;

        public Task10_SendNewOrder(PlcDriverRegistry driverRegistry, PlcChannelRegistry channelRegistry, ProductHashGenerator hashGenerator, RecipeRepository recipeRepo, ILogger<Task10_SendNewOrder> logger)
        {
            _driverRegistry = driverRegistry;
            _channelRegistry = channelRegistry;
            _hashGenerator = hashGenerator;
            _recipeRepo = recipeRepo;
            _logger = logger;
        }

        public async Task<bool> ExecuteAsync(int plcId, ProductionOrder order)
        {

            // 1. Pobranie driver PLC (obszar "WriteOrder")
            var plcDriver = _driverRegistry.GetDriver(plcId);

            if (plcDriver == null || !plcDriver.IsConnected)
            {
                _logger.LogError("PLC {PlcId} jest rozłączone. Nie można wysłać zlecenia.", plcId);
                return false;
            }

            using (LogContext.PushProperty("PlcName", plcDriver.PlcName))
            {
                try
                {
                    _logger.LogInformation("Rozpoczynam realizację [Task 10] zlecenia {OrderId} dla maszyny {PlcId}", order.KOLZLEC, plcId);
                    // 2. ODCISK PALCA: Generujemy unikalny Hash geometrii
                    string productHash = _hashGenerator.GenerateHash(order);
                    _logger.LogInformation("Wygenerowano ProductHash: {Hash}, dla zlecenia {OrderId}", productHash, order.KOLZLEC);

                    // 3. BAZA DANYCH: Zabezpieczamy istnienie produktu i pobieramy ewentualną recepturę
                    await _recipeRepo.EnsureProductDefinitionExistsAsync(productHash, order);

                    // 4. MAPOWANIE KONFIGURACJI DO PLC
                    SiemensConfigData plcConfig = await _recipeRepo.GetConfigForProductAsync(productHash, plcId)
                                                  ?? new SiemensConfigData { Status = 2 }; // Nowy produkt, puste zera

                    if (plcConfig.Status == 1)
                        _logger.LogInformation("Znaleziono recepturę dla produktu hash = {Hash}. (Status 1)", productHash);
                    else
                        _logger.LogInformation("Produkt nowy/bez receptury na tym PLC. Wysłano domyślne parametry zerowe (Status 2).");


                    // 5. Mapowanie: SQL -> PLC
                    var plcOrder = order.MapOrderToPLC();


                    // 6. Zapis danych zlecenia
                    bool writeOrderSuccess = await plcDriver.WriteAreaAsync("WriteOrder", plcOrder);
                    if (!writeOrderSuccess)
                    {
                        _logger.LogError("Nie udało się zapisać bloku zlecenia [WriteOrder] do PLC {PlcId}.", plcId);
                        return false;
                    }

                    // 7. Zapis danych konfiguracji
                    bool writeConfigSuccess = await plcDriver.WriteAreaAsync("WriteConfig", plcConfig);
                    if (!writeConfigSuccess)
                    {
                        _logger.LogError("Nie udało się zapisać bloku danych [WriteConfig] do PLC {PlcId}.", plcId);
                        return false;
                    }

                    // 8. Wysłanie Task 10 i OCZEKIWANIE na maszynę stanów (handshake)
                    var channel = _channelRegistry.GetChannel(plcId);
                    if (channel == null) return false;

                    var taskRequest = new PlcTaskRequest(10); // Tworzymy żądanie zadania nr 10
                    await channel.Writer.WriteAsync(taskRequest); // Wrzucamy do maszyny stanów

                    _logger.LogInformation("Wystawiono task 10. Oczekiwanie na potwierdzenie od PLC {PlcId}.", plcId);

                    // TaskCompletionSource: Wątek zatrzymuje się tutaj i czeka,
                    // aż pętla 100ms w PlcWorkerService wykona SetResult(true)!
                    bool isConfirmedByPlc = await taskRequest.Tcs.Task;

                    if (isConfirmedByPlc)
                    {
                        _logger.LogInformation("SUKCES! Zlecenie {NrZlec} (Hash: {Hash}) uruchomione na maszynie {PlcId}.", order.KOLZLEC, productHash, plcId);
                        // Opcjonalnie: Zapis faktu startu zlecenia do SQL (Tabela OrdersHistory) można wywołać tutaj
                        return true;
                    }

                    return false;
                }
                catch (TimeoutException)
                {
                    _logger.LogError("Timeout! PLC {PlcId} nie potwierdziło pobrania zlecenia 10 w określonym czasie.", plcId);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Krytyczny błąd podczas wysyłania zlecenia. PLC {PlcId}.", plcId);
                    return false;
                }
            }
        }
    }
}
