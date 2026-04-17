using AutomatedSolutions.ASCommStd.SI.S7.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Models;

[StructLayout(LayoutKind.Sequential)]
public class SiemensConfigData : UDT
{
    // Status operacji: 
    // Do PLC: 1 = Znaleziono w bazie, 2 = Nowy produkt (brak w bazie)
    // Od PLC: Możesz użyć np. jako flagi poprawności odczytu/zapisu
    public Int16 Status;

    // Tablica 100 zmiennych REAL (ASComm natywnie wspiera mapowanie tablic w UDT)
    public Single[] Parameters = new Single[100];

    public SiemensConfigData()
    {
        // Inicjalizacja tablicy jest wymagana, by ASComm mógł poprawnie obliczyć wielkość paczki bajtów
        for (int i = 0; i < Parameters.Length; i++)
        {
            Parameters[i] = 0.0f;
        }
    }
}