using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services.RecipeManager;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class Task50_SaveRecipeHandler : IInboundTaskHandler
{
    public int TaskId => 50;

    private readonly PlcDriverRegistry _driverRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Task50_SaveRecipeHandler> _logger;

    public Task50_SaveRecipeHandler(
            PlcDriverRegistry driverRegistry,
            IServiceScopeFactory scopeFactory,
            ILogger<Task50_SaveRecipeHandler> logger)
    {
        _driverRegistry = driverRegistry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData)
    {
        _logger.LogInformation("Rozpoczęto obsługę Task 50 (Zapis Receptury) dla PLC {PlcId}...", plcId);

        var driver = _driverRegistry.GetDriver(plcId);
        if (driver == null || !driver.IsConnected)
        {
            _logger.LogError("Błąd Task 50: PLC {PlcId} jest rozłączone.", plcId);
            return false;
        }

        // 1. Zrzut pełnej pamięci z maszyny (Aktualne zlecenie + Parametry)
        var currentOrder = await driver.ReadAreaAsync<SiemensOrderData>("ReadOrder");
        var currentConfig = await driver.ReadAreaAsync<SiemensConfigData>("ReadConfig");

        if (currentOrder == null || currentConfig == null)
        {
            _logger.LogError("Błąd Task 50: Odczyt bloków ReadOrder lub ReadConfig zwrócił null.");
            return false;
        }

        try
        {
            // 2. Tworzymy krótki Scope dla serwisów bazodanowych
            using var scope = _scopeFactory.CreateScope();
            var hashGen = scope.ServiceProvider.GetRequiredService<ProductHashGenerator>();
            var recipeRepo = scope.ServiceProvider.GetRequiredService<RecipeRepository>();

            // 3. Generujemy Hash bezpośrednio ze struktury, która "siedzi" w PLC
            string productHash = hashGen.GenerateHash(currentOrder);


            // 4. Zapisujemy najpierw definicję zlecenia (geometrię), jeśli jeszcze jej nie ma w bazie

            await recipeRepo.EnsureProductDefinitionExistsAsync(productHash, currentOrder);

            string operatorName = $"Maszyna {plcId} (Panel HMI)";

            bool isSaved = await recipeRepo.SaveConfigFromPlcAsync(productHash, plcId, currentConfig, operatorName);

            if (isSaved)
            {
                _logger.LogInformation("SUKCES Task 50: Receptura dla Hash {Hash} zaktualizowana w SQL.", productHash);
                return true; // Wszystko OK, router ustawi Error_Status = 0
            }
            else
            {
                _logger.LogWarning("BŁĄD Task 50: Repozytorium zwróciło błąd podczas zapisu dla Hash {Hash}.", productHash);
                return false; // Błąd w SQL, router ustawi Error_Status = 1
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisu Task 50 do lokalnej bazy danych!");
            return false;
        }
    }
}
