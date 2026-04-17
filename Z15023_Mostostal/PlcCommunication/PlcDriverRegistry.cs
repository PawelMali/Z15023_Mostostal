using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.PlcCommunication.Drivers;

namespace Z25023_Mostostal.PlcCommunication
{
    public class PlcDriverRegistry
    {
        private readonly ConcurrentDictionary<int, IPlcDriver> _drivers = new();

        public void RegisterDriver(int plcId, IPlcDriver driver)
        {
            _drivers[plcId] = driver;
        }

        public IPlcDriver? GetDriver(int plcId)
        {
            _drivers.TryGetValue(plcId, out var driver);
            return driver;
        }
    }
}
