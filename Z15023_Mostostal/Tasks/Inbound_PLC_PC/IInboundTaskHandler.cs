using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.PlcCommunication.Models;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public interface IInboundTaskHandler
{
    // Numer taska (np. 50)
    int TaskId { get; }

    // Metoda wykonująca logikę
    // Zwraca TRUE jeśli logika wykonała się bez błędów
    Task<bool> ExecuteAsync(int plcId, SiemensReadData statusData);
}
