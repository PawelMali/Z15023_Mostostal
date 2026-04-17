using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.State;

namespace Z25023_Mostostal.Tasks.Outbound_PC_PLC
{
    public class OutboundTaskDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        // Wstrzykujemy czysty kontener .NET
        public OutboundTaskDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // Wywołanie z interfejsu wygląda tak: await _taskManager.SendNewOrderAsync(1, order);
        public async Task<bool> SendTask10_NewOrderAsync(int plcId, ProductionOrder order)
        {
            // Kontener tworzy instancję klasy zadania TYLKO w momencie wywołania

            var dataStore = _serviceProvider.GetRequiredService<PlcDataStore>();
            dataStore.AddTaskLog(plcId, $"Wysłano zlec. {order.NRZLEC} (Task 10)");

            var task = _serviceProvider.GetRequiredService<Task10_SendNewOrder>();
            bool result = await task.ExecuteAsync(plcId, order);

            string statusText = result ? "ZAAKCEPTOWANE" : "ODRZUCONE/TIMEOUT";
            dataStore.AddTaskLog(plcId, $"Wynik Task 10: {statusText}");

            return result;
        }
    }
}
