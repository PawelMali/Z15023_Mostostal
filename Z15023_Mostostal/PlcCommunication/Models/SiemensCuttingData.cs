using AutomatedSolutions.ASCommStd.SI.S7.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public class SiemensCuttingData : UDT
    {
        public SiemensCuttingStep[] steps = new SiemensCuttingStep[50];

        public SiemensCuttingData()
        {
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = new SiemensCuttingStep();
            }
        }
    }
}
