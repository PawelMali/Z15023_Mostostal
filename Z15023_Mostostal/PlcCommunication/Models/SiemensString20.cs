using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Models;

public class SiemensString20 // 1. Zmiana nazwy klasy (opcjonalna, ale zalecana dla porządku)
{
    // Maksymalna długość znaków to 20. Wartość stała ułatwiająca zarządzanie.
    private const int STRING_LENGTH = 20;

    #region Fields

    public byte MaxLength;    // Bajt 0 w S7
    public byte ActualLength; // Bajt 1 w S7

    // 2. Zmiana SizeConst na pożądaną długość
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = STRING_LENGTH)]
    public Byte[] data;

    #endregion Fields

    public SiemensString20()
    {
        // 3. Inicjalizacja tablicy nowym rozmiarem
        data = new byte[STRING_LENGTH];
    }

    #region Methods

    public override string ToString()
    {
        if (data == null) return string.Empty;

        int safeLen = ActualLength;

        if (safeLen > data.Length)
            safeLen = data.Length;

        if (MaxLength > 0 && safeLen > MaxLength && MaxLength <= data.Length)
            safeLen = MaxLength;

        if (safeLen < 0) safeLen = 0;

        try
        {
            return Encoding.ASCII.GetString(data, 0, safeLen);
        }
        catch
        {
            return string.Empty;
        }
    }

    public void SetString(string value)
    {
        if (value == null) value = string.Empty;

        // 4. Zmiana przycinania na nowy rozmiar
        if (value.Length > STRING_LENGTH)
            value = value.Substring(0, STRING_LENGTH);

        ActualLength = (byte)value.Length;

        // 5. Ustawienie domyślnego MaxLength na nową wartość
        if (MaxLength == 0) MaxLength = STRING_LENGTH;

        byte[] bytes = Encoding.ASCII.GetBytes(value);

        Array.Clear(data, 0, data.Length);
        Array.Copy(bytes, data, bytes.Length);
    }

    #endregion Methods
}
