using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Models;

[StructLayout(LayoutKind.Sequential)]
public class SiemensString
{
    #region Fields

    public byte MaxLength;    // Bajt 0 w S7
    public byte ActualLength; // Bajt 1 w S7

    // Stały bufor danych. MarshalAs zapewnia, że ASComm wie, ile bajtów czytać.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 254)]
    public Byte[] data;

    #endregion Fields

    public SiemensString()
    {
        // Inicjalizacja, aby uniknąć błędów null reference przy tworzeniu obiektu ręcznie
        data = new byte[254];
    }

    #region Methods

    public override string ToString()
    {
        // ZABEZPIECZENIE PRZED ŚMIECIAMI Z PAMIĘCI
        if (data == null) return string.Empty;

        // Bierzemy długość z bajtu nagłówka
        int safeLen = ActualLength;

        // 1. Walidacja względem fizycznego bufora
        if (safeLen > data.Length)
            safeLen = data.Length;

        // 2. Walidacja względem nagłówka MaxLength (jeśli PLC ma poprawne dane)
        // Czasami przy błędach pamięci MaxLength też może być śmieciem, więc polegamy głównie na data.Length
        if (MaxLength > 0 && safeLen > MaxLength && MaxLength <= data.Length)
            safeLen = MaxLength;

        // 3. Ostateczne bezpieczniki
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

        // Przycinamy string do maksymalnego rozmiaru bufora S7 (zazwyczaj 254 znaki)
        if (value.Length > 254)
            value = value.Substring(0, 254);

        // Ustawiamy nagłówki zgodnie ze standardem S7
        ActualLength = (byte)value.Length;

        // Jeśli MaxLength nie jest ustawione (nowy obiekt), ustawiamy domyślne 254
        if (MaxLength == 0) MaxLength = 254;

        // Konwersja i zapis do bufora
        byte[] bytes = Encoding.ASCII.GetBytes(value);

        // Czyścimy bufor (opcjonalne, ale zalecane dla czystości pamięci PLC)
        Array.Clear(data, 0, data.Length);

        // Kopiujemy nowe dane
        Array.Copy(bytes, data, bytes.Length);
    }

    #endregion Methods
}
