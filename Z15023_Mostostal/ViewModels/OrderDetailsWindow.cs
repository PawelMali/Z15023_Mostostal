using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication.Models;

namespace Z25023_Mostostal.ViewModels;

public partial class OrderDetailsViewModel : ObservableObject
{
    [ObservableProperty] private ProductionOrder _data;




    public OrderDetailsViewModel(SiemensOrderData data)
    {
        _data = new ProductionOrder();
        _data.SetOrderFromPLC(data);

    }
}
