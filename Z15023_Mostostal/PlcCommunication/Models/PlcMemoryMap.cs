using System;
using System.Collections.Generic;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Models
{
    public class PlcMemoryMap
    {
        public string ReadData { get; set; } = string.Empty;   // np. "DB10.DBB0"
        public string WriteData { get; set; } = string.Empty;  // np. "DB11.DBB0"
        public string ReadOrder { get; set; } = string.Empty;
        public string WriteOrder { get; set; } = string.Empty;
        public string ReadResults { get; set; } = string.Empty;
        public string WriteResults { get; set; } = string.Empty;
        public string ReadConfig { get; set; } = string.Empty;
        public string WriteConfig { get; set; } = string.Empty;
        public string WriteCuttingData { get; set; } = string.Empty;
    }
}
