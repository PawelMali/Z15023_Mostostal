using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows;
using Z25023_Mostostal.Settings;
using Z25023_Mostostal.Settings.Models;

namespace Z25023_Mostostal.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsManagerService _settingsManager;

    // Pola zbindowane do interfejsu UI
    [ObservableProperty] private string _ordersViewName;

    // Właściwości dla Bazy Głównej (ERP)
    [ObservableProperty] private string _server;
    [ObservableProperty] private string _port;
    [ObservableProperty] private string _databaseName;
    [ObservableProperty] private string _username;

    //Właściwości dla Lokalnej Bazy
    [ObservableProperty] private string _localServer;
    [ObservableProperty] private string _localPort;
    [ObservableProperty] private string _localDatabaseName;
    [ObservableProperty] private string _localUsername;








    // Hasło trzymamy osobno. Będzie aktualizowane z Code-Behind (ze względów bezpieczeństwa WPF)
    public string NewPassword { get; set; } = string.Empty;
    public string LocalNewPassword { get; set; } = string.Empty;

    public Action? CloseWindowAction { get; set; }

    public SettingsViewModel(SettingsManagerService settingsManager)
    {
        _settingsManager = settingsManager;

        // Inicjalizacja pól na podstawie aktualnej konfiguracji z RAM
        var currentDb = _settingsManager.CurrentConfig.Database;
        _server = currentDb.Server;
        _port = currentDb.Port.ToString();
        _databaseName = currentDb.DatabaseName;
        _username = currentDb.Username;
        _ordersViewName = currentDb.OrdersViewName;

        var currentLocalDb = _settingsManager.CurrentConfig.LocalDatabase;
        _localServer = currentLocalDb.Server;
        _localPort = currentLocalDb.Port.ToString();
        _localDatabaseName = currentLocalDb.DatabaseName;
        _localUsername = currentLocalDb.Username;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        try
        {
            // Budujemy tymczasowy connection string do testu
            string passwordToUse = string.IsNullOrEmpty(NewPassword)
                ? _settingsManager.GetDecryptedDatabasePassword()
                : NewPassword;

            string testConnString = $"Server={Server},{Port};Database={DatabaseName};User Id={Username};Password={passwordToUse};TrustServerCertificate=True;Connection Timeout=3;";

            using var connection = new SqlConnection(testConnString);
            await connection.OpenAsync();

            if (connection.State == ConnectionState.Open)
            {
                MessageBox.Show("Połączenie z bazą danych zakończone sukcesem!", "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd połączenia:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task TestLocalConnectionAsync()
    {
        try
        {
            string passwordToUse = string.IsNullOrEmpty(LocalNewPassword)
                ? _settingsManager.GetDecryptedLocalDatabasePassword()
                : LocalNewPassword;

            string testConnString = $"Server={LocalServer},{LocalPort};Database={LocalDatabaseName};User Id={LocalUsername};Password={passwordToUse};TrustServerCertificate=True;Connection Timeout=3;";

            using var connection = new SqlConnection(testConnString);
            await connection.OpenAsync();

            if (connection.State == ConnectionState.Open)
                MessageBox.Show("Połączenie z Lokalną Bazą Danych zakończone sukcesem!", "Test OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd połączenia z Lokalną Bazą:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        try
        {
            // Walidacja portu
            if (!int.TryParse(Port, out int portNumber))
            {
                MessageBox.Show("Port musi być liczbą całkowitą.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(LocalPort, out int localPortNumber)) 
            {
                MessageBox.Show("Port musi być liczbą całkowitą.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Klonujemy obecną konfigurację i nadpisujemy nowymi wartościami
            var newConfig = new AppConfig();
            newConfig.Database = new DatabaseSettings
            {
                Server = this.Server,
                Port = portNumber,
                DatabaseName = this.DatabaseName,
                Username = this.Username,
                // Kopiujemy stare zaszyfrowane hasło (zostanie nadpisane w serwisie, jeśli podano nowe)
                EncryptedPassword = _settingsManager.CurrentConfig.Database.EncryptedPassword
            };

            newConfig.LocalDatabase = new DatabaseSettings
            {
                Server = this.LocalServer,
                Port = localPortNumber,
                DatabaseName = this.LocalDatabaseName,
                Username = this.LocalUsername,
                EncryptedPassword = _settingsManager.CurrentConfig.LocalDatabase.EncryptedPassword
            };

            // Zapisujemy na dysk (serwis zajmie się szyfrowaniem NewPassword, jeśli nie jest puste)
            _settingsManager.SaveSettings(newConfig, NewPassword, LocalNewPassword);

            MessageBox.Show("Ustawienia zostały zapisane. Aplikacja użyje nowej konfiguracji natychmiast.", "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
            CloseWindowAction?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nie udało się zapisać ustawień:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseWindowAction?.Invoke();
    }
}