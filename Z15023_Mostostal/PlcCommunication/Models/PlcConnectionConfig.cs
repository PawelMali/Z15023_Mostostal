using System;
using System.Collections.Generic;
using System.Text;

namespace Z15023_Mostostal.PlcCommunication.Models
{
    public class PlcConnectionConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public short Rack { get; set; }
        public short Slot { get; set; }

        public PlcMemoryMap MemoryMap { get; set; } = new();
    }
}
