using System;
using System.Collections.Generic;
using System.Text;

namespace Z25023_Mostostal.PlcCommunication
{
    public class PlcTaskRequest
    {
        public short TaskId { get; }

        // To jest nasza "obietnica" zakończenia zadania. 
        // RunContinuationsAsynchronously zapobiega blokowaniu wątku PLC przez UI.
        public TaskCompletionSource<bool> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PlcTaskRequest(short taskId)
        {
            TaskId = taskId;
        }
    }
}
