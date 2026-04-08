using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Z15023_Mostostal.ViewModels
{
    // Reprezentuje stan pojedynczego sterownika (Bindowane do UserControl)
    public partial class PlcViewModel : ObservableObject
    {
        [ObservableProperty] private int _plcId;
        [ObservableProperty] private string _machineName = string.Empty;

        // Zmienne procesowe z ASComm
        [ObservableProperty] private bool _autoMode;
        [ObservableProperty] private bool _manualMode;
        [ObservableProperty] private bool _autoActive;
        [ObservableProperty] private bool _alarm;
        [ObservableProperty] private bool _warning;
        [ObservableProperty] private short _partCounter;

        // Dodatkowy status (np. jeśli PLC przestanie odpowiadać)
        [ObservableProperty] private bool _isConnected = true;
    }
}
