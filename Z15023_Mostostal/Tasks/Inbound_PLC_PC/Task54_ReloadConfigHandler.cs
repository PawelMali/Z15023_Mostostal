using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services.RecipeManager;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class Task54_ReloadConfigHandler : IInboundTaskHandler
{
    public int TaskId => 54;

    private readonly PlcDriverRegistry _driverRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Task54_ReloadConfigHandler> _logger;

    public Task54_ReloadConfigHandler(
        PlcDriverRegistry driverRegistry,
        IServiceScopeFactory scopeFactory,
        ILogger<Task54_ReloadConfigHandler> logger)
    {
        _driverRegistry = driverRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData)
    {
        _logger.LogInformation("Rozpoczęto obsługę Task 54 (Pobranie konfiguracji dla aktualnego zlecenia) dla PLC {PlcId}...", plcId);

        var driver = _driverRegistry.GetDriver(plcId);
        if (driver == null || !driver.IsConnected)
        {
            _logger.LogError("Błąd Task 54: PLC {PlcId} jest rozłączone.", plcId);
            return false;
        }

        // 1. Pobieramy zrzut aktualnego zlecenia (geometrii), nad którym pracuje maszyna
        var currentOrderData = await driver.ReadAreaAsync<SiemensOrderData>("ReadOrder");

        if (currentOrderData == null || string.IsNullOrWhiteSpace(currentOrderData.KOLZLEC.ToString()))
        {
            _logger.LogWarning("Błąd Task 54: Brak aktywnego zlecenia na PLC {PlcId}.", plcId);
            return false; // Router ustawi Error_Status = 1 w PLC
        }

        try
        {
            // 2. Tworzymy wyizolowany scope dla serwisów bazodanowych
            using var scope = _scopeFactory.CreateScope();
            var hashGen = scope.ServiceProvider.GetRequiredService<ProductHashGenerator>();
            var recipeRepo = scope.ServiceProvider.GetRequiredService<RecipeRepository>();

            // 3. Generujemy Hash bezpośrednio na podstawie parametrów ze sterownika
            string productHash = hashGen.GenerateHash(currentOrderData);

            // 4. Pobieramy recepturę (100 parametrów) z lokalnej bazy SQL
            var plcConfig = await recipeRepo.GetConfigForProductAsync(productHash, plcId);

            if (plcConfig == null)
            {
                _logger.LogWarning("Task 54: Brak zapisanej receptury dla zlecenia {OrderNo} (Hash: {Hash}). Wysłano pustą tablicę.", currentOrderData.KOLZLEC, productHash);
                plcConfig = new SiemensConfigData { Status = 2 }; // Flaga 2 = Nowy produkt (puste zera)
            }
            else
            {
                plcConfig.Status = 1; // Flaga 1 = Znaleziono w bazie
                _logger.LogInformation("Task 54: Znaleziono recepturę dla zlecenia {OrderNo}.", currentOrderData.KOLZLEC);
            }

            // 5. Odsyłamy pobrane parametry z powrotem do PLC (do bloku danych konfiguracyjnych)
            bool writeSuccess = await driver.WriteAreaAsync("WriteConfig", plcConfig);

            if (writeSuccess)
            {
                _logger.LogInformation("SUKCES Task 54: Parametry technologiczne przesłane do PLC {PlcId}.", plcId);
                return true; // Sukces Handshake, Error_Status = 0
            }
            else
            {
                _logger.LogError("Błąd Task 54: Nie udało się zapisać bloku WriteConfig do PLC {PlcId}.", plcId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas procesowania Task 54 (Baza Danych) dla PLC {PlcId}!", plcId);
            return false;
        }
    }
}
