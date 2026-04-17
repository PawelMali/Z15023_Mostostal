using System;
using System.Collections.Generic;
using System.Text;

namespace Z25023_Mostostal.Models;

public class PlcParameterItem
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
}
