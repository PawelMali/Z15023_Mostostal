using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Z15023_Mostostal.Tasks
{
    public class PlcTaskManager
    {
        private readonly IServiceProvider _serviceProvider;

        // Wstrzykujemy czysty kontener .NET
        public PlcTaskManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        // Wywołanie z interfejsu wygląda tak: await _taskManager.SendNewOrderAsync(1);
        public async Task<bool> SendNewOrderAsync(int plcId)
        {
            // Kontener tworzy instancję klasy zadania TYLKO w momencie wywołania
            var task = _serviceProvider.GetRequiredService<Task10_SendNewOrder>();
            return await task.ExecuteAsync(plcId);
        }
    }
}
