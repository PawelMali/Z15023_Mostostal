using System;
using System.Collections.Generic;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication.Drivers
{
    public interface IPlcDriver : IDisposable
    {
        string PlcName { get; }
        bool IsConnected { get; }
        bool HasConfigurationError { get; }

        Task<bool> ConnectAsync();

        // Przekazujemy logiczną nazwę obszaru np. "ReadData", "WriteOrder"
        Task<T?> ReadAreaAsync<T>(string areaName) where T : class, new();
        Task<bool> WriteAreaAsync<T>(string areaName, T data) where T : class;
    }
}
