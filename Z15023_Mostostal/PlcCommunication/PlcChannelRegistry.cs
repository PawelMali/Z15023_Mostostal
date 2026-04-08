using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace Z15023_Mostostal.PlcCommunication
{
    public class PlcChannelRegistry
    {
        // Słownik przechowujący kanały nadawcze (To PLC) dla każdego sterownika
        private readonly ConcurrentDictionary<int, Channel<PlcTaskRequest>> _toPlcChannels = new();

        public void RegisterChannel(int plcId, Channel<PlcTaskRequest> channel)
        {
            _toPlcChannels[plcId] = channel;
        }

        public Channel<PlcTaskRequest>? GetChannel(int plcId)
        {
            _toPlcChannels.TryGetValue(plcId, out var channel);
            return channel;
        }
    }
}
