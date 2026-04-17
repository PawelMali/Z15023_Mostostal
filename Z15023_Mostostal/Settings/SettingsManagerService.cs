using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.IO;
using Z25023_Mostostal.Settings.Models;
using Z25023_Mostostal.Settings.Security;

namespace Z25023_Mostostal.Settings;

public class SettingsManagerService
{
    private readonly ICryptoService _cryptoService;
    private readonly ILogger<SettingsManagerService> _logger;

    // Plik będzie zawsze obok pliku .exe aplikacji
    private readonly string _settingsFilePath;

    // Zmienna trzymająca aktualną konfigurację w pamięci RAM
    private AppConfig _currentConfig = new();

    public SettingsManagerService(ICryptoService cryptoService, ILogger<SettingsManagerService> logger)
    {
        _cryptoService = cryptoService;
        _logger = logger;

        // Ustalenie bezpiecznej ścieżki (AppDomain.CurrentDomain.BaseDirectory wskazuje na folder z .exe)
        _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "localsettings.json");

        // Ładujemy ustawienia od razu przy starcie serwisu
        LoadSettings();
    }

    /// <summary>
    /// Zwraca aktualną konfigurację z pamięci (tylko do odczytu dla interfejsu).
    /// </summary>
    public AppConfig CurrentConfig => _currentConfig;

    /// <summary>
    /// Wczytuje plik .json. Jeśli nie istnieje, tworzy go z domyślnymi wartościami.
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    _currentConfig = config;
                    _logger.LogInformation("Konfiguracja z pliku localsettings.json wczytana pomyślnie.");
                    return;
                }
            }

            _logger.LogWarning("Brak pliku konfiguracji lub plik pusty. Tworzenie domyślnych ustawień.");
            SaveSettings(_currentConfig); // Zapisuje domyślny plik na dysk
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Krytyczny błąd podczas wczytywania localsettings.json. Użyto ustawień domyślnych.");
        }
    }

    /// <summary>
    /// Zapisuje konfigurację na dysk. Jeśli podano nowe hasło jawne, szyfruje je.
    /// </summary>
    public void SaveSettings(AppConfig newConfig, string? newPlainTextPassword = null, string? newLocalPlainTextPassword = null)
    {
        try
        {
            // Jeśli użytkownik wpisał w oknie nowe hasło, szyfrujemy je
            if (!string.IsNullOrWhiteSpace(newPlainTextPassword))
                newConfig.Database.EncryptedPassword = _cryptoService.Encrypt(newPlainTextPassword);
            // Jeśli hasło pozostało puste (użytkownik go nie zmieniał), zostawiamy stare zaszyfrowane

            // To samo dla lokalnej bazy danych
            if (!string.IsNullOrWhiteSpace(newLocalPlainTextPassword))
                newConfig.LocalDatabase.EncryptedPassword = _cryptoService.Encrypt(newLocalPlainTextPassword);

            var options = new JsonSerializerOptions { WriteIndented = true }; // Plik będzie ładnie sformatowany
            string json = JsonSerializer.Serialize(newConfig, options);

            File.WriteAllText(_settingsFilePath, json);

            // Aktualizujemy konfigurację w pamięci
            _currentConfig = newConfig;

            _logger.LogInformation("Konfiguracja została pomyślnie zaktualizowana i zapisana na dysku.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisywania do pliku localsettings.json.");
            throw; // Rzucamy dalej, aby okno UI mogło wyświetlić MessageBox o błędzie
        }
    }


    // Pobiera ODSZYFROWANE hasło (tylko do użytku wewnętrznego, np. dla Dappera).
    public string GetDecryptedDatabasePassword()
    {
        return _cryptoService.Decrypt(_currentConfig.Database.EncryptedPassword);
    }

    // Buduje i zwraca kompletny ConnectionString dla SQL Server.
    public string GetSqlConnectionString()
    {
        var db = _currentConfig.Database;
        string plainPassword = GetDecryptedDatabasePassword();

        // Standardowy ciąg połączeniowy do MS SQL Server / SQL Express
        return $"Server={db.Server},{db.Port};Database={db.DatabaseName};User Id={db.Username};Password={plainPassword};TrustServerCertificate=True;";
    }

    public string GetDecryptedLocalDatabasePassword()
    {
        return _cryptoService.Decrypt(_currentConfig.LocalDatabase.EncryptedPassword);
    }

    public string GetLocalSqlConnectionString()
    {
        var db = _currentConfig.LocalDatabase;
        string plainPassword = GetDecryptedLocalDatabasePassword();
        return $"Server={db.Server},{db.Port};Database={db.DatabaseName};User Id={db.Username};Password={plainPassword};TrustServerCertificate=True;";
    }
}
