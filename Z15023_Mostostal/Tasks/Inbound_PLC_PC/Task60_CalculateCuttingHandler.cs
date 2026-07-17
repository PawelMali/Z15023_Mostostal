using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Z25023_Mostostal.PlcCommunication;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal_Cięcie.Core; // Wykorzystujemy silnik matematyczny z projektu cięcia

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class Task60_CalculateCuttingHandler : IInboundTaskHandler
{
    public int TaskId => 60;

    private readonly PlcDriverRegistry _driverRegistry;
    private readonly ILogger<Task60_CalculateCuttingHandler> _logger;

    public Task60_CalculateCuttingHandler(PlcDriverRegistry driverRegistry, ILogger<Task60_CalculateCuttingHandler> logger)
    {
        _driverRegistry = driverRegistry;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData)
    {
        _logger.LogInformation("Rozpoczęto obsługę Task 60 (Przeliczenie i optymalizacja pętli cięcia) dla PLC {PlcId}...", plcId);

        var driver = _driverRegistry.GetDriver(plcId);
        if (driver == null || !driver.IsConnected)
        {
            _logger.LogError("Błąd Task 60: Sterownik dla PLC {PlcId} jest rozłączony.", plcId);
            return false;
        }

        // 1. Pobieramy aktualne dane zlecenia znajdujące się w buforze maszyny
        var currentOrder = await driver.ReadAreaAsync<SiemensOrderData>("ReadOrder");
        if (currentOrder == null || string.IsNullOrWhiteSpace(currentOrder.KOLZLEC.ToString()))
        {
            _logger.LogError("Błąd Task 60: Nie można pobrać danych ReadOrder ze sterownika.");
            return false;
        }

        try
        {
            // 2. Mapowanie parametrów inżynieryjnych na konfigurację technologiczną detalu
            double length = Math.Round(currentOrder.DRZECZ_BBL, 2);
            double pitch = Math.Round(currentOrder.OCZKOL_CBS, 2);
            double marginLeft = Math.Round(currentOrder.PSKRAJDL_TEF, 2);
            double marginRight = Math.Round(currentOrder.PSKRAJDL2_DEF, 2);

            // Dokładne sprawdzenie trybu seratacji (tylko gdy typ zawiera "X5" lub "X7") po normalizacji x5 lub x7
            string typ = currentOrder.TYP?.ToString()?.Trim() ?? "";

            Z25023_Mostostal_Cięcie.Core.CuttingType cuttingType = 
                    (currentOrder.TFT.ToString() == "bednarka" )
                    ? Z25023_Mostostal_Cięcie.Core.CuttingType.T
                    : Z25023_Mostostal_Cięcie.Core.CuttingType.P;


            bool isSerration = typ.Contains("x5") || typ.Contains("x7");


            // 3. Uruchomienie bezpiecznej symulacji matematycznej w tle
            var machineConfig = MachineConfig.Load() with
            {
                EnableSerration = isSerration
            };
            var detailConfig = new DetailConfig(length, marginLeft, marginRight, pitch, cuttingType);
            var logic = new ProductionLogic(machineConfig);

            int optimalChunks;
            double optimalKnifePosition = logic.CalculateOptimalKnifePosition(detailConfig, out optimalChunks);

            // Generujemy pełne kroki produkcyjne prasy
            var allSteps = logic.GenerateProductionSteps(detailConfig, 4, optimalKnifePosition, optimalChunks);

            // 4. Detekcja i wyciąganie Zgeneralizowanej Pętli (Steady State) na podstawie rzazów gilotyny
            var cutIndices = new List<int>();
            for (int i = 0; i < allSteps.Count; i++)
            {
                if (allSteps[i].IsCutActive) cutIndices.Add(i);
            }

            List<SimulationStep> steadyStateSteps = null;
            if (cutIndices.Count >= 3)
            {
                int startIndex = cutIndices[1] + 1; // Start tuż po drugim cięciu
                int endIndex = cutIndices[2];       // Koniec na trzecim cięciu
                steadyStateSteps = allSteps.GetRange(startIndex, endIndex - startIndex + 1);
            }
            else if (cutIndices.Count == 2)
            {
                int startIndex = cutIndices[0] + 1;
                int endIndex = cutIndices[1];
                steadyStateSteps = allSteps.GetRange(startIndex, endIndex - startIndex + 1);
            }

            if (steadyStateSteps == null || steadyStateSteps.Count == 0)
            {
                _logger.LogWarning("Błąd Task 60: Algorytm nie wyznaczył stabilnego cyklu Steady State.");
                return false;
            }

            // 5. Przygotowanie danych strukturalnych i pakowanie do klasy SiemensCuttingData
            var cuttingData = new SiemensCuttingData();

            // Przepisujemy wyliczone kroki zachowując bezpieczne limity tablicy (max 50)
            int stepsToTransfer = Math.Min(steadyStateSteps.Count, cuttingData.steps.Length);
            for (int j = 0; j < stepsToTransfer; j++)
            {
                var step = steadyStateSteps[j];
                cuttingData.steps[j].Lp = (short)(j + 1);
                cuttingData.steps[j].Delta = (float)step.StepDisplacement; // Przesuw w pętli
                cuttingData.steps[j].CutPosition = (float)step.CutTargetX;
                cuttingData.steps[j].Punch = (int)step.PunchesMask;        // Maska bitowa stempli
                cuttingData.steps[j].Cut = (short)(step.IsCutActive ? 1 : 0);
            }

            // 6. Transmisja gotowego bloku danych zgeneralizowanej pętli do pamięci PLC
            bool success = await driver.WriteAreaAsync("WriteCuttingData", cuttingData);

            if (success)
            {
                _logger.LogInformation("SUKCES Task 60: Przesłano {Count} kroków profilu cięcia dla zlecenia {OrderNo}.", stepsToTransfer, currentOrder.KOLZLEC);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wyjątek krytyczny w trakcie przetwarzania potoku Task 60 dla PLC {PlcId}", plcId);
            return false;
        }
    }
}