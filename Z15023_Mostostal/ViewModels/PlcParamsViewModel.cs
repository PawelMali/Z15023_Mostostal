using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Z25023_Mostostal.Models;
using Z25023_Mostostal.PlcCommunication.Models;
using Z25023_Mostostal.Services.RecipeManager;

namespace Z25023_Mostostal.ViewModels;

public partial class PlcParamsViewModel : ObservableObject
{
    public ObservableCollection<PlcParameterItem> Parameters { get; } = new();

    public PlcParamsViewModel(int plcId, SiemensConfigData configData, ParameterDefinitionService paramService)
    {
        if (configData == null || configData.Parameters == null) return;

        // Pętla od 0 do 99 - łączymy wartość z jej nazwą z pliku CSV
        for (int i = 0; i < configData.Parameters.Length; i++)
        {
            Parameters.Add(new PlcParameterItem
            {
                Index = i,
                Name = paramService.GetParameterName(plcId, i),
                Value = configData.Parameters[i]
            });
        }
    }
}
