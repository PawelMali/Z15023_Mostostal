using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionApp.PlcCommunication;

// Maszyna stanów dla zadań przychodzących Z PLC DO PC
public enum FromPlcState
{
    Idle,
    ProcessingInApp,     // PC przetwarza zadanie (np. szuka w SQL)
    WaitingForPlcReset,  // PC potwierdził, czeka aż PLC wyzeruje żądanie
    Error
}
