using System;
using System.Runtime.InteropServices;
using AutomatedSolutions.ASCommStd.SI.S7.Data;

namespace Z15023_Mostostal.PlcCommunication.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public class SiemensOrderData : UDT
    {
        public Int32 Order_ID;           // Unikalny numer zlecenia
        public Int32 Product_Code;       // Kod produktu
        public Int32 Target_Quantity;    // Ilość do wyprodukowania
        public float Target_Temperature; // Przykładowy parametr procesu

        // Rezerwy dla wyrównania ramki
        public Int16 Reserve1;
        public Int16 Reserve2;
    }
}
