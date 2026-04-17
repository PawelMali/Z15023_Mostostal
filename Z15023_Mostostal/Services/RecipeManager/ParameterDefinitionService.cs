using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Z25023_Mostostal.Services.RecipeManager;

public class ParameterDefinitionService
{
    private readonly ILogger<ParameterDefinitionService> _logger;

    // Zewnętrzny klucz: PlcId (np. 1-4)
    // Wewnętrzny słownik: Index (0-99) -> Nazwa parametru
    private readonly ConcurrentDictionary<int, Dictionary<int, string>> _plcParameters = new();

    public ParameterDefinitionService(ILogger<ParameterDefinitionService> logger)
    {
        _logger = logger;
        LoadAllDefinitions();
    }

    private void LoadAllDefinitions()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Szukamy wszystkich plików pasujących do wzorca "parameters_*.csv"
        var files = Directory.GetFiles(baseDirectory, "parameters_*.csv");

        if (files.Length == 0)
        {
            _logger.LogWarning("Nie znaleziono żadnych plików CSV z parametrami (oczekiwany format np.: parameters_1.csv).");
            return;
        }

        foreach (var filePath in files)
        {
            try
            {
                // Ekstrakcja ID sterownika z nazwy pliku (np. "parameters_1.csv" -> 1)
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string idPart = fileName.Replace("parameters_", "");

                if (int.TryParse(idPart, out int plcId))
                {
                    var parameterNames = new Dictionary<int, string>();

                    foreach (var line in File.ReadAllLines(filePath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var parts = line.Split(';');
                        // Oczekujemy formatu: Indeks;Nazwa
                        if (parts.Length >= 2 && int.TryParse(parts[0], out int index))
                        {
                            parameterNames[index] = parts[1].Trim();
                        }
                    }

                    _plcParameters[plcId] = parameterNames;

                    _logger.LogInformation("Załadowano {Count} definicji dla PLC {PlcId} z pliku {FileName}",
                        parameterNames.Count, plcId, Path.GetFileName(filePath));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd odczytu pliku CSV: {FilePath}", filePath);
            }
        }
    }

    /// <summary>
    /// Zwraca nazwę parametru dla danego sterownika i indeksu.
    /// Jeśli brak definicji, generuje bezpieczną nazwę awaryjną.
    /// </summary>
    public string GetParameterName(int plcId, int index)
    {
        // Najpierw szukamy słownika dla konkretnej maszyny
        if (_plcParameters.TryGetValue(plcId, out var parameterNames))
        {
            // Następnie szukamy w niej konkretnego indeksu
            if (parameterNames.TryGetValue(index, out var name))
            {
                return name;
            }
        }

        // Fallback, gdy brak pliku dla danego PLC lub brakuje indeksu w pliku CSV
        return $"Zmienna pusta {index}";
    }
}
