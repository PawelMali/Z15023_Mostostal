using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services;
using Z25023_Mostostal.Services.RecipeManager;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class Task51_HandleOrderRequest : IInboundTaskHandler
{
    public int TaskId => 51;

    private readonly PlcDriverRegistry _driverRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Task51_HandleOrderRequest> _logger;

    public Task51_HandleOrderRequest(
        PlcDriverRegistry driverRegistry,
        IServiceScopeFactory scopeFactory,
        ILogger<Task51_HandleOrderRequest> logger)
    {
        _driverRegistry = driverRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData)
    {
        // 1. Pobieramy numer zlecenia ze struktury, którą właśnie odczytał Worker co 100ms
        string requestedOrderNo = statusData.OrderNumberReq.ToString();
        string requestedOrderPositionNo = statusData.OrderPositionReq.ToString();

        _logger.LogInformation("PLC {PlcId} żąda danych dla zlecenia: {OrderNo}, {PositionNo}", plcId, requestedOrderNo, requestedOrderPositionNo);

        if (string.IsNullOrWhiteSpace(requestedOrderNo))
        {
            _logger.LogWarning("Otrzymano puste żądanie numeru zlecenia od PLC {PlcId}.", plcId);
            return false; // To ustawi Error_Status = 1
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orderRepo = scope.ServiceProvider.GetRequiredService<OrderRepository>();
            var recipeRepo = scope.ServiceProvider.GetRequiredService<RecipeRepository>();
            var hashGen = scope.ServiceProvider.GetRequiredService<ProductHashGenerator>();

            // 2. Szukamy zlecenia w bazie ERP (Widok SQL)
            var orderFromErp = await orderRepo.GetOrderByNumberAsync(requestedOrderNo, requestedOrderPositionNo);

            if (orderFromErp == null)
            {
                _logger.LogWarning("Zlecenie {OrderNo} nie istnieje w bazie ERP.", requestedOrderNo);
                return false;
            }

            // 3. Reszta logiki identyczna jak w Task 10
            string productHash = hashGen.GenerateHash(orderFromErp);

            // Zabezpieczamy istnienie definicji produktu w bazie lokalnej
            await recipeRepo.EnsureProductDefinitionExistsAsync(productHash, orderFromErp);

            // Pobieramy recepturę (płaskie 100 kolumn)
            var plcConfig = await recipeRepo.GetConfigForProductAsync(productHash, plcId)
                            ?? new SiemensConfigData { Status = 2 };

            // 4. Mapujemy dane zlecenia do struktury Siemensa
            var plcOrder = orderFromErp.MapOrderToPLC();

            // 5. Wpisujemy dane do PLC (bloki WriteOrder i WriteConfig)
            var driver = _driverRegistry.GetDriver(plcId);
            bool w1 = await driver.WriteAreaAsync("WriteConfig", plcConfig);
            bool w2 = await driver.WriteAreaAsync("WriteOrder", plcOrder);

            if (w1 && w2)
            {
                _logger.LogInformation("Dane dla zlecenia {OrderNo} wysłane pomyślnie na żądanie PLC {PlcId}.", requestedOrderNo, plcId);
                return true; // Sukces Handshake
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas procesowania Task 51 dla PLC {PlcId}", plcId);
            return false;
        }
    }
}
