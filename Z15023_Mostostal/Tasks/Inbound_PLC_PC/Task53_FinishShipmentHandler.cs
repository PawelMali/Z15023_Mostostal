using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class Task53_FinishShipmentHandler : IInboundTaskHandler
{
    public int TaskId => 53;

    private readonly PlcDriverRegistry _driverRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Task53_FinishShipmentHandler> _logger;

    public Task53_FinishShipmentHandler(
        PlcDriverRegistry driverRegistry,
        IServiceScopeFactory scopeFactory,
        ILogger<Task53_FinishShipmentHandler> logger)
    {
        _driverRegistry = driverRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData)
    {
        _logger.LogInformation("Rozpoczęto obsługę Task 53 (Zakończenie przesyłki) dla PLC {PlcId}...", plcId);

        var driver = _driverRegistry.GetDriver(plcId);
        if (driver == null || !driver.IsConnected)
        {
            _logger.LogError("Błąd Task 53: PLC {PlcId} jest rozłączone.", plcId);
            return false;
        }

        // 1. Pobieramy aktualne zlecenie, nad którym pracuje maszyna
        var currentOrderData = await driver.ReadAreaAsync<SiemensOrderData>("ReadOrder");

        if (currentOrderData == null || string.IsNullOrWhiteSpace(currentOrderData.KOLZLEC.ToString()))
        {
            _logger.LogWarning("Błąd Task 53: Brak aktywnego zlecenia na PLC {PlcId}.", plcId);
            return false; // Ustawi Error_Status = 1 w PLC
        }

        // 2. Mapujemy strukturę PLC na model C#
        var order = new ProductionOrder();
        order.SetOrderFromPLC(currentOrderData);

        try
        {
            // 3. Tworzymy scope dla bezpiecznego, wyizolowanego użycia bazy danych
            using var scope = _scopeFactory.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<OrderRepository>();

            await orderRepo.InsertCompletedShipmentAsync(order, plcId);

            _logger.LogInformation("SUKCES Task 53: Zapisano zakończenie przesyłki {Przesylka} (Faktor: {Faktor}) dla zlecenia {OrderNo} na PLC {PlcId}.",
                order.PRZESYLKA, order.FAKTOR, order.KOLZLEC, plcId);

            return true; // Sukces Handshake, router ustawi Error_Status = 0 w pamięci PLC
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisu Task 53 do bazy danych!");
            return false; // Błąd Handshake
        }
    }
}
