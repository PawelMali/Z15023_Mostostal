using System;
using System.Runtime.InteropServices;
using AutomatedSolutions.ASCommStd.SI.S7.Data;

namespace Z25023_Mostostal.PlcCommunication.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public class SiemensReadData : UDT
    {
        public Int16 Life;
        public UInt16 Task_Counter;
        public Int16 Task_Send_To_PC;
        public Int16 Task_Confirm_To_PC;
        public UInt16 Task_Succes_Counter;
        public Int16 Error_Status;
        public bool AutoMode;
        public bool ManualMode; 
        public bool AutoActive;
        public bool Alarm;
        public bool Warning;
        public bool ReserveB5;
        public bool ReserveB6;
        public bool ReserveB7;
        public bool ReserveB8;
        public bool ReserveB9;
        public bool ReserveB10;
        public bool ReserveB11;
        public bool ReserveB12;
        public bool ReserveB13;
        public bool ReserveB14;
        public bool ReserveB15;

        public Int16 PartCounter;
        public Int16 Reserve3;
        public Int16 Reserve4;
        public Int16 Reserve5;
        public SiemensString20 OrderNumberReq = new SiemensString20();  
    }

}
