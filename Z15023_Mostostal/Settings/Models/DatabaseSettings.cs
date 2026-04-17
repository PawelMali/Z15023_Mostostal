using System;
using System.Collections.Generic;
using System.Text;

namespace Z25023_Mostostal.Settings.Models;

// 1. Model ustawień specyficznych dla bazy danych
public class DatabaseSettings
{
    public string Server { get; set; } = ".\\SQLEXPRESS";
    public int Port { get; set; } = 1433;
    public string DatabaseName { get; set; } = "ProductionDB";
    public string Username { get; set; } = "sa";
    public string OrdersViewName { get; set; } = "KRATY_PROZAP";

    // Przechowujemy w modelu TYLKO zaszyfrowaną wersję hasła.
    // Hasło jawnym tekstem nigdy nie powinno znajdować się w obiekcie, 
    // który będzie bezpośrednio serializowany do JSON.
    public string EncryptedPassword { get; set; } = string.Empty;
}

