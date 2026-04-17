using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;
using Z25023_Mostostal.Models;

namespace Z25023_Mostostal.Services.RecipeManager;

public class ProductHashGenerator
{
    /// <summary>
    /// Generuje unikalny odcisk palca geometrii detalu na podstawie zlecenia z ERP.
    /// Jeśli wymiary lub typ się zmienią, wygenerowany zostanie zupełnie inny ciąg.
    /// </summary>
    public string GenerateHash(ProductionOrder order)
    {
        // 1. Zbieramy wszystkie cechy decydujące o fizycznym wymiarze i materiale
        // Ważne: Kolejność zmiennych musi być zawsze taka sama.
        // Formatujemy double używając InvariantCulture, aby uniknąć problemów przecinek/kropka 
        // na komputerach z różnymi ustawieniami regionalnymi Windows.
        var format = System.Globalization.CultureInfo.InvariantCulture;

        string fingerprintData = string.Join("|",
            order.TYP,
            order.DLUGOSC.ToString("0.00", format),
            order.SZEROKOSC.ToString("0.00", format),
            order.DRZECZ.ToString("0.00", format),
            order.SZRZECZ.ToString("0.00", format),
            order.OCZKOH.ToString("0.00", format),
            order.OCZKOL.ToString("0.00", format),
            order.PSKRAJSZER.ToString("0.00", format),
            order.PSKRAJSZER2.ToString("0.00", format),
            order.PSKRAJDL.ToString("0.00", format),
            order.PSKRAJDL2.ToString("0.00", format),
            order.PLASKH.ToString("0.00", format),
            order.PLASKS.ToString("0.00", format),
            order.HWALC.ToString("0.00", format),
            order.SWALC.ToString("0.00", format),
            order.TFT,
            order.THTH.ToString("0.00", format),
            order.TFTF.ToString("0.00", format),
            order.TFD,
            order.TFDH.ToString("0.00", format),
            order.TFDF.ToString("0.00", format),
            order.TFL,
            order.TFLH.ToString("0.00", format),
            order.TFLF.ToString("0.00", format),
            order.TFR,
            order.TFRH.ToString("0.00", format),
            order.TFRF.ToString("0.00", format)
        );

        // 2. Hashowanie ciągu
        byte[] bytes = Encoding.UTF8.GetBytes(fingerprintData);
        byte[] hash = SHA256.HashData(bytes);

        // 3. Konwersja do czytelnego, krótkiego formatu HEX (64 znaki)
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Generuje odcisk palca bezpośrednio ze struktury odczytanej z pamięci sterownika (Task 50).
    /// </summary>
    public string GenerateHash(PlcCommunication.Models.SiemensOrderData plcOrder)
    {
        var format = System.Globalization.CultureInfo.InvariantCulture;

        // Używamy .Value dla pól SiemensString
        string fingerprintData = string.Join("|",

            plcOrder.TYP.ToString(),
            plcOrder.DLUGOSC.ToString("0.00", format),
            plcOrder.SZEROKOSC.ToString("0.00", format),
            plcOrder.DRZECZ_BBL.ToString("0.00", format),
            plcOrder.SZRZECZ_CBL.ToString("0.00", format),
            plcOrder.OCZKOH_BBS.ToString("0.00", format),
            plcOrder.OCZKOL_CBS.ToString("0.00", format),
            plcOrder.PSKRAJSZER_LEF.ToString("0.00", format),
            plcOrder.PSKRAJSZER2_REF.ToString("0.00", format),
            plcOrder.PSKRAJDL_TEF.ToString("0.00", format),
            plcOrder.PSKRAJDL2_DEF.ToString("0.00", format),
            plcOrder.PLASKH_BBH.ToString("0.00", format),
            plcOrder.PLASKS_BBT.ToString("0.00", format),
            plcOrder.HWALC_CBH.ToString("0.00", format),
            plcOrder.SWALC_CBT.ToString("0.00", format),
            plcOrder.TFT.ToString(),
            plcOrder.THTH.ToString("0.00", format),
            plcOrder.TFTF.ToString("0.00", format),
            plcOrder.TFD.ToString(),
            plcOrder.TFDH.ToString("0.00", format),
            plcOrder.TFDF.ToString("0.00", format),
            plcOrder.TFL.ToString(),
            plcOrder.TFLH.ToString("0.00", format),
            plcOrder.TFLF.ToString("0.00", format),
            plcOrder.TFR.ToString(),
            plcOrder.TFRH.ToString("0.00", format),
            plcOrder.TFRF.ToString("0.00", format)
        );

        byte[] bytes = Encoding.UTF8.GetBytes(fingerprintData);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
