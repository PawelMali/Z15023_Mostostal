using System;
using System.Collections.Generic;
using System.Text;

namespace Z25023_Mostostal.Settings.Models;

//Główny korzeń konfiguracji (AppConfig)
public class AppConfig
{
    // Baza główna (ERP) - skąd pobieramy widok zleceń
    public DatabaseSettings Database { get; set; } = new();

    // Baza lokalna - gdzie trzymamy receptury (Config) i historię (OrdersDone)
    public DatabaseSettings LocalDatabase { get; set; } = new()
    {
        DatabaseName = "Z25023_DB" // Domyślna inna nazwa
    };

    // Przestrzeń na przyszłość. Będziesz mógł tu łatwo dodawać kolejne moduły:
    // public ScannerSettings Scanners { get; set; } = new();
    // public EmailSettings EmailAlerts { get; set; } = new();
}
