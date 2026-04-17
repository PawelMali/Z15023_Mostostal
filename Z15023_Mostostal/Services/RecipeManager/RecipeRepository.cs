using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Settings;

namespace Z25023_Mostostal.Services.RecipeManager;

public class RecipeRepository
{
    private readonly SettingsManagerService _settings;
    private readonly ILogger<RecipeRepository> _logger;

    public RecipeRepository(SettingsManagerService settings, ILogger<RecipeRepository> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Zapisuje definicję produktu do bazy lokalnej (jeśli jeszcze nie istnieje).
    /// </summary>
    public async Task EnsureProductDefinitionExistsAsync(string productHash, ProductionOrder order)
    {
        try
        {
            string connStr = _settings.GetLocalSqlConnectionString();
            using var connection = new SqlConnection(connStr);
            // Prosty UPSERT / IF NOT EXISTS (zabezpiecza przed błędem klucza obcego w przyszłości)
            string sql = @"
                IF NOT EXISTS (SELECT 1 FROM ProductDefinitions WHERE ProductHash = @ProductHash)
                BEGIN
                    INSERT INTO ProductDefinitions 
                    (ProductHash, KOLZLEC, CZESC, PRZESYLKA, FAKTOR, SZTUKPOZ, POZ_WYKWYS,
                     TYP, DLUGOSC, SZEROKOSC, DRZECZ, SZRZECZ, OCZKOH, OCZKOL, 
                     PSKRAJSZER, PSKRAJSZER2, PSKRAJDL, PSKRAJDL2, PLASKH, PLASKS, 
                     HWALC, SWALC, SZTPLN, SZPLL, SZTKR, TFT, THTH, TFTF, 
                     TFD, TFDH, TFDF, TFL, TFLH, TFLF, TFR, TFRH, TFRF)
                    VALUES 
                    (@ProductHash, @KOLZLEC, @CZESC, @PRZESYLKA, @FAKTOR, @SZTUKPOZ, @POZ_WYKWYS,
                     @TYP, @DLUGOSC, @SZEROKOSC, @DRZECZ, @SZRZECZ, @OCZKOH, @OCZKOL, 
                     @PSKRAJSZER, @PSKRAJSZER2, @PSKRAJDL, @PSKRAJDL2, @PLASKH, @PLASKS, 
                     @HWALC, @SWALC, @SZTPLN, @SZPLL, @SZTKR, @TFT, @THTH, @TFTF, 
                     @TFD, @TFDH, @TFDF, @TFL, @TFLH, @TFLF, @TFR, @TFRH, @TFRF)
                END";

            // Dapper automatycznie zmapuje parametry z obiektu 'order' (nazwy właściwości pasują)
            // Dodajemy tylko ProductHash, którego nie ma w oryginalnym 'order'
            var parameters = new DynamicParameters(order);
            parameters.Add("@ProductHash", productHash);

            await connection.ExecuteAsync(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisywania definicji produktu do bazy lokalnej.");
        }
    }

    /// <summary>
    /// PRZECIĄŻENIE: Zapisuje definicję produktu na podstawie danych odebranych z PLC (Task 50).
    /// Mapuje nazwy pól PLC (np. DRZECZ_BBL) na nazwy kolumn SQL (DRZECZ).
    /// </summary>
    public async Task EnsureProductDefinitionExistsAsync(string productHash, SiemensOrderData plcOrder)
    {
        try
        {
            string connStr = _settings.GetLocalSqlConnectionString();
            using var connection = new SqlConnection(connStr);

            string sql = @"
                IF NOT EXISTS (SELECT 1 FROM ProductDefinitions WHERE ProductHash = @ProductHash)
                BEGIN
                    INSERT INTO ProductDefinitions 
                    (ProductHash, KOLZLEC, CZESC, PRZESYLKA, FAKTOR, SZTUKPOZ, POZ_WYKWYS,
                     TYP, DLUGOSC, SZEROKOSC, DRZECZ, SZRZECZ, OCZKOH, OCZKOL, 
                     PSKRAJSZER, PSKRAJSZER2, PSKRAJDL, PSKRAJDL2, PLASKH, PLASKS, 
                     HWALC, SWALC, SZTPLN, SZPLL, SZTKR, TFT, THTH, TFTF, 
                     TFD, TFDH, TFDF, TFL, TFLH, TFLF, TFR, TFRH, TFRF)
                    VALUES 
                    (@ProductHash, @KOLZLEC, @CZESC, @PRZESYLKA, @FAKTOR, @SZTUKPOZ, @POZ_WYKWYS,
                     @TYP, @DLUGOSC, @SZEROKOSC, @DRZECZ, @SZRZECZ, @OCZKOH, @OCZKOL, 
                     @PSKRAJSZER, @PSKRAJSZER2, @PSKRAJDL, @PSKRAJDL2, @PLASKH, @PLASKS, 
                     @HWALC, @SWALC, @SZTPLN, @SZPLL, @SZTKR, @TFT, @THTH, @TFTF, 
                     @TFD, @TFDH, @TFDF, @TFL, @TFLH, @TFLF, @TFR, @TFRH, @TFRF)
                END";

            var parameters = new DynamicParameters();
            parameters.Add("@ProductHash", productHash);

            // Mapowanie pól (zdejmujemy .Value z SiemensString i rzutujemy Single na float/double)
            parameters.Add("@KOLZLEC", plcOrder.KOLZLEC.ToString());
            parameters.Add("@CZESC", (int)plcOrder.CZESC);
            parameters.Add("@PRZESYLKA", (int)plcOrder.PRZESYLKA);
            parameters.Add("@FAKTOR", (int)plcOrder.FAKTOR);
            parameters.Add("@SZTUKPOZ", (int)plcOrder.SZTUKPOZ);
            parameters.Add("@POZ_WYKWYS", (int)plcOrder.POZ_WYKWYS);
            parameters.Add("@TYP", plcOrder.TYP.ToString());
            parameters.Add("@DLUGOSC", (double)plcOrder.DLUGOSC);
            parameters.Add("@SZEROKOSC", (double)plcOrder.SZEROKOSC);
            parameters.Add("@DRZECZ", (double)plcOrder.DRZECZ_BBL);
            parameters.Add("@SZRZECZ", (double)plcOrder.SZRZECZ_CBL);
            parameters.Add("@OCZKOH", (double)plcOrder.OCZKOH_BBS);
            parameters.Add("@OCZKOL", (double)plcOrder.OCZKOL_CBS);
            parameters.Add("@PSKRAJSZER", (double)plcOrder.PSKRAJSZER_LEF);
            parameters.Add("@PSKRAJSZER2", (double)plcOrder.PSKRAJSZER2_REF);
            parameters.Add("@PSKRAJDL", (double)plcOrder.PSKRAJDL_TEF);
            parameters.Add("@PSKRAJDL2", (double)plcOrder.PSKRAJDL2_DEF);
            parameters.Add("@PLASKH", (double)plcOrder.PLASKH_BBH);
            parameters.Add("@PLASKS", (double)plcOrder.PLASKS_BBT);
            parameters.Add("@HWALC", (double)plcOrder.HWALC_CBH);
            parameters.Add("@SWALC", (double)plcOrder.SWALC_CBT);
            parameters.Add("@SZTPLN", (int)plcOrder.SZTPLN_BBN);
            parameters.Add("@SZPLL", (int)plcOrder.SZPLL_CBN);
            parameters.Add("@SZTKR", (int)plcOrder.SZTKR_PCS);
            parameters.Add("@TFT", plcOrder.TFT.ToString());
            parameters.Add("@THTH", (double)plcOrder.THTH);
            parameters.Add("@TFTF", (double)plcOrder.TFTF);
            parameters.Add("@TFD", plcOrder.TFD.ToString());
            parameters.Add("@TFDH", (double)plcOrder.TFDH);
            parameters.Add("@TFDF", (double)plcOrder.TFDF);
            parameters.Add("@TFL", plcOrder.TFL.ToString());
            parameters.Add("@TFLH", (double)plcOrder.TFLH);
            parameters.Add("@TFLF", (double)plcOrder.TFLF);
            parameters.Add("@TFR", plcOrder.TFR.ToString());
            parameters.Add("@TFRH", (double)plcOrder.TFRH);
            parameters.Add("@TFRF", (double)plcOrder.TFRF);

            await connection.ExecuteAsync(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisywania definicji produktu z danych PLC do bazy lokalnej.");
        }
    }

    /// <summary>
    /// Pobiera zapisane parametry procesowe (100 Realów) dla danego hasha i PLC.
    /// </summary>
    /// <summary>
    /// Pobiera 100 parametrów płaskich i od razu zwraca gotową strukturę dla PLC.
    /// Zwraca null, jeśli produkt nie ma jeszcze zapisanej receptury na tym PLC.
    /// </summary>
    public async Task<SiemensConfigData?> GetConfigForProductAsync(string productHash, int plcId)
    {
        try
        {
            string connStr = _settings.GetLocalSqlConnectionString();
            using var connection = new SqlConnection(connStr);

            string sql = "SELECT * FROM ProcessConfigs WHERE ProductHash = @Hash AND PlcId = @PlcId";

            // Pobieramy jako typ 'dynamic' (Dapper zwróci swój obiekt DapperRow)
            var dynamicRow = await connection.QuerySingleOrDefaultAsync(sql, new { Hash = productHash, PlcId = plcId });

            if (dynamicRow != null)
            {
                // DapperRow natywnie implementuje interfejs IDictionary, więc bezpiecznie rzutujemy
                var row = (IDictionary<string, object>)dynamicRow;

                var config = new SiemensConfigData { Status = 1 }; // 1 = Znaleziono

                // Pętla od 0 do 99 mapująca kolumny Param0..Param99 na tablicę w C#
                for (int i = 0; i < 100; i++)
                {
                    if (row.TryGetValue($"Param{i}", out var val) && val != null)
                    {
                        config.Parameters[i] = Convert.ToSingle(val);
                    }
                }
                return config;
            }

            return null; // Brak w bazie = Nowy produkt
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania receptury dla Hash: {Hash}", productHash);
            return null;
        }
    }

    /// <summary>
    /// Zapisuje pełną konfigurację z PLC (Task 11) do płaskiej tabeli.
    /// Zostanie użyte w Kroku 5.
    /// </summary>
    public async Task<bool> SaveConfigFromPlcAsync(string productHash, int plcId, SiemensConfigData plcConfig, string operatorName)
    {
        try
        {
            string connStr = _settings.GetLocalSqlConnectionString();
            using var connection = new SqlConnection(connStr);

            // Generowanie zapytania UPSERT (Update jeśli istnieje, Insert jeśli nowe)
            var dp = new DynamicParameters();
            dp.Add("@Hash", productHash);
            dp.Add("@PlcId", plcId);
            dp.Add("@Operator", operatorName);

            var columns = new List<string>();
            var values = new List<string>();
            var updates = new List<string>();

            for (int i = 0; i < 100; i++)
            {
                string paramName = $"Param{i}";
                string varName = $"@{paramName}";

                columns.Add(paramName);
                values.Add(varName);
                updates.Add($"{paramName} = {varName}");

                dp.Add(varName, plcConfig.Parameters[i]);
            }

            string sql = $@"
                IF EXISTS (SELECT 1 FROM ProcessConfigs WHERE ProductHash = @Hash AND PlcId = @PlcId)
                BEGIN
                    UPDATE ProcessConfigs 
                    SET LastUpdatedAt = GETDATE(), UpdatedByOperator = @Operator, 
                        {string.Join(", ", updates)}
                    WHERE ProductHash = @Hash AND PlcId = @PlcId
                END
                ELSE
                BEGIN
                    INSERT INTO ProcessConfigs 
                    (ProductHash, PlcId, UpdatedByOperator, {string.Join(", ", columns)})
                    VALUES 
                    (@Hash, @PlcId, @Operator, {string.Join(", ", values)})
                END";

            await connection.ExecuteAsync(sql, dp);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd zapisu receptury do bazy dla Hash: {Hash}", productHash);
            return false;
        }
    }
}
