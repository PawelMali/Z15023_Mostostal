using AutomatedSolutions.ASCommStd.SI.S7.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public class SiemensCuttingStep : UDT
    {
        public Int16 Lp; //Nr. kroku
        public Single Delta;  //Przesunięcie
        public Single CutPosition;  //Pozycja noża
        public int Punch; // Maska stempli
        public Int16 Cut; // 1 - cięcie aktywne, 0 - cięcie nieaktywne
    }
}
