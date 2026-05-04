using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class Task52_UpdateProductionHandler : IInboundTaskHandler
{
    public int TaskId => 52;

    private readonly PlcDriverRegistry _driverRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Task52_UpdateProductionHandler> _logger;

    public Task52_UpdateProductionHandler(
        PlcDriverRegistry driverRegistry,
        IServiceScopeFactory scopeFactory,
        ILogger<Task52_UpdateProductionHandler> logger)
    {
        _driverRegistry = driverRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData)
    {
        _logger.LogInformation("Rozpoczęto obsługę Task 52 (Aktualizacja Produkcji) dla PLC {PlcId}...", plcId);

        var driver = _driverRegistry.GetDriver(plcId);
        if (driver == null || !driver.IsConnected)
        {
            _logger.LogError("Błąd Task 52: PLC {PlcId} jest rozłączone.", plcId);
            return false;
        }

        // 1. Pobieramy aktualne zlecenie, nad którym pracuje maszyna
        var currentOrderData = await driver.ReadAreaAsync<SiemensOrderData>("ReadOrder");

        if (currentOrderData == null || string.IsNullOrWhiteSpace(currentOrderData.KOLZLEC.ToString()))
        {
            _logger.LogWarning("Błąd Task 52: Brak aktywnego zlecenia na PLC {PlcId}.", plcId);
            return false; // Ustawi Error_Status = 1 w PLC
        }

        // Mapujemy obiekt Siemensa na nasz model C# (żeby łatwiej przekazać do Dappera)
        var order = new ProductionOrder();
        order.SetOrderFromPLC(currentOrderData);

        // 2. Pobieramy aktualny licznik ze zmiennych "szybkich"
        int currentProductionCounter = statusData.PartCounter;

        try
        {
            // 3. Otwieramy krótki Scope dla bazy danych i zapisujemy stan
            using var scope = _scopeFactory.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<OrderRepository>();

            await orderRepo.UpdateProductionCounterAsync(order, currentProductionCounter, plcId);

            _logger.LogInformation("SUKCES Task 52: Zaktualizowano licznik ({Counter} szt.) dla zlecenia {OrderNo} na PLC {PlcId}.",
                currentProductionCounter, order.KOLZLEC, plcId);

            return true; // Sukces Handshake, router ustawi Error_Status = 0
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisu Task 52 do bazy danych!");
            return false; // Błąd Handshake
        }
    }
}
