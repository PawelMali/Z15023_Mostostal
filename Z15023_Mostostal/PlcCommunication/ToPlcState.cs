using System;
using System.Collections.Generic;
using System.Text;

namespace Z15023_Mostostal.PlcCommunication
{
    // Maszyna stanów dla zadań wychodzących Z PC DO PLC
    public enum ToPlcState
    {
        Idle,
        WaitingForPlcConfirm, // PC wysłał, czeka na potwierdzenie pobrania przez PLC
        WaitingForPlcReset,   // PC wyzerował, czeka na wyzerowanie potwierdzenia przez PLC
        Error
    }
}
