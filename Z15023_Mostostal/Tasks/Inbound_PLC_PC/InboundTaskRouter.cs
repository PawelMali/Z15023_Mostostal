using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.PlcCommunication.Models;

namespace Z25023_Mostostal.Tasks.Inbound_PLC_PC;

public class InboundTaskRouter
{
    private readonly Dictionary<int, IInboundTaskHandler> _handlers;
    private readonly ILogger<InboundTaskRouter> _logger;

    // Dependency Injection: wstrzykuje IEnumerable wszystkich klas
    // implementujących IInboundTaskHandler i buduje z nich słownik O(1).
    public InboundTaskRouter(IEnumerable<IInboundTaskHandler> handlers, ILogger<InboundTaskRouter> logger)
    {
        _handlers = handlers.ToDictionary(h => h.TaskId);
        _logger = logger;
    }

    public async Task<bool> RouteAsync(int taskId, int plcId, SiemensReadData statusData)
    {
        if (_handlers.TryGetValue(taskId, out var handler))
        {
            return await handler.ExecuteAsync(plcId, statusData);
        }

        _logger.LogWarning("Otrzymano nieznany Task {TaskId} od PLC {PlcId}! Brak zarejestrowanego handlera.", taskId, plcId);
        return false;
    }
}
