using System;
using System.Collections.Generic;
using System.Text;
using Z25023_Mostostal.PlcCommunication.Models;

namespace Z25023_Mostostal.Models;

public class ProductionOrder
{
    public string KOLZLEC { get; set; } = string.Empty; //Nr. Zlecenia
    public string CZESC { get; set; } = string.Empty;     //Numer częsci zlecenia
    public int PRZESYLKA { get; set; }  //Przesyłka dotycząca paletyzacji
    public short FAKTOR { get; set; }     //Krotnośc przesyłki -  ilość sztuk paletyzacji
    public double SZTUKPOZ { get; set; }   //Ilość krat na przesyłkę
    public double POZ_WYKWYS { get; set; } //LP.
    public string TYP { get; set; } = string.Empty; //Typ kraty
    public double DLUGOSC { get; set; }     //Długość kraty
    public double SZEROKOSC { get; set; }   //Szerokość kraty
    public double DRZECZ { get; set; }  //Długość płaskownika nośnego - BBL
    public double SZRZECZ { get; set; } //Długość płaskownika Łączącego - CBL
    public double OCZKOH { get; set; }  //Podziałka pomiędzy płaskownikami nośnymi - BBS
    public double OCZKOL { get; set; }  //Podziałka pomiędzy płaskownikami łączącymi - CBS
    public double PSKRAJSZER { get; set; }  //Pole skrajne lewe łączący - LEF
    public double PSKRAJSZER2 { get; set; } //Pole skrajne prawe łączący - REF
    public double PSKRAJDL { get; set; }    //Pole skrajne górne nośny - TEF
    public double PSKRAJDL2 { get; set; }   //Pole skrajne dolne nosny - DEF
    public double PLASKH { get; set; }  //Wysokość płaskownika nośnego - BBH
    public double PLASKS { get; set; }  //Grubość płaskownika noścnego - BBT
    public double HWALC { get; set; }   //Wysokość płaskownika łączącego - CBH
    public double SWALC { get; set; }   //Grubość płaskownika łączącego - CBT
    public double SZTPLN { get; set; } //Ilość płaskowników nośnych - BBN
    public double SZPLL { get; set; }  //Ilość płaskowników łaczących - CBN
    public double SZTKR { get; set; }  //Sztuk krat do wykonania - PCS
    public int TFT { get; set; }         //Typ obramowania góra - TFT
    public double THTH { get; set; }    //Wysokość obramowania góra - THTH
    public double TFTF { get; set; }    //Grubość obramowania góra - TFTF
    public int TFD { get; set; }         //Typ obramowania dół - TFD
    public double TFDH { get; set; }    //Wysokość obramowania dół - TFDH
    public double TFDF { get; set; }    //Grubość obramowania dół - TFDF
    public int TFL { get; set; }         //Typ obramowania lewo - TFL
    public double TFLH { get; set; }    //Wysokość obramowania lewo - TFLH
    public double TFLF { get; set; }    //Grubość obramowania lewo - TFLF
    public int TFR { get; set; }         //Typ obramowania prawo - TFR
    public double TFRH { get; set; }    //Wysokość obramowania prawo - TFRH 
    public double TFRF { get; set; }    //Grubość obramowania prawo - TFRF
    public string NRZLEC { get; set; } = string.Empty;  //Numer zlecenia ??


    public SiemensOrderData MapOrderToPLC()
    {
        SiemensOrderData siemensOrderData = new SiemensOrderData();

        siemensOrderData.KOLZLEC.SetString(KOLZLEC);
        siemensOrderData.CZESC.SetString(CZESC);
        siemensOrderData.PRZESYLKA = (short)PRZESYLKA;
        siemensOrderData.FAKTOR = (short)FAKTOR;
        siemensOrderData.SZTUKPOZ = (short)SZTUKPOZ;
        siemensOrderData.POZ_WYKWYS = (short)POZ_WYKWYS;
        siemensOrderData.TYP.SetString(TYP);
        siemensOrderData.DLUGOSC = (float)DLUGOSC;
        siemensOrderData.SZEROKOSC = (float)SZEROKOSC;
        siemensOrderData.DRZECZ_BBL = (float)DRZECZ;
        siemensOrderData.SZRZECZ_CBL = (float)SZRZECZ;
        siemensOrderData.OCZKOH_BBS = (float)OCZKOH;
        siemensOrderData.OCZKOL_CBS = (float)OCZKOL;
        siemensOrderData.PSKRAJSZER_LEF = (float)PSKRAJSZER;
        siemensOrderData.PSKRAJSZER2_REF = (float)PSKRAJSZER2;
        siemensOrderData.PSKRAJDL_TEF = (float)PSKRAJDL;
        siemensOrderData.PSKRAJDL2_DEF = (float)PSKRAJDL2;
        siemensOrderData.PLASKH_BBH = (float)PLASKH;
        siemensOrderData.PLASKS_BBT = (float)PLASKS;
        siemensOrderData.HWALC_CBH = (float)HWALC;
        siemensOrderData.SWALC_CBT = (float)SWALC;
        siemensOrderData.SZTPLN_BBN = (short)SZTPLN;
        siemensOrderData.SZPLL_CBN = (short)SZPLL;
        siemensOrderData.SZTKR_PCS = (short)SZTKR;
        siemensOrderData.TFT = (short)TFT;
        siemensOrderData.THTH = (float)THTH;
        siemensOrderData.TFTF = (float)TFTF;
        siemensOrderData.TFD = (short)TFD;
        siemensOrderData.TFDH = (float)TFDH;
        siemensOrderData.TFDF = (float)TFDF;
        siemensOrderData.TFL = (short)TFL;
        siemensOrderData.TFLH = (float)TFLH;
        siemensOrderData.TFLF = (float)TFLF;
        siemensOrderData.TFR = (short)TFR;
        siemensOrderData.TFRH = (float)TFRH;
        siemensOrderData.TFRF = (float)TFRF;
        siemensOrderData.NRZLEC.SetString(NRZLEC);

        return siemensOrderData;
    }

    public void SetOrderFromPLC(SiemensOrderData siemensOrderData)
    {
        KOLZLEC = siemensOrderData.KOLZLEC.ToString();
        CZESC = siemensOrderData.CZESC.ToString();
        PRZESYLKA = siemensOrderData.PRZESYLKA;
        FAKTOR = siemensOrderData.FAKTOR;
        SZTUKPOZ = Math.Round(siemensOrderData.SZTUKPOZ, 2);
        POZ_WYKWYS = Math.Round(siemensOrderData.POZ_WYKWYS, 2);
        TYP = siemensOrderData.TYP.ToString();
        DLUGOSC = Math.Round(siemensOrderData.DLUGOSC, 2);
        SZEROKOSC = Math.Round(siemensOrderData.SZEROKOSC, 2);
        DRZECZ = Math.Round(siemensOrderData.DRZECZ_BBL, 2);
        SZRZECZ = Math.Round(siemensOrderData.SZRZECZ_CBL, 2);
        OCZKOH = Math.Round(siemensOrderData.OCZKOH_BBS, 2);
        OCZKOL = Math.Round(siemensOrderData.OCZKOL_CBS, 2);
        PSKRAJSZER = Math.Round(siemensOrderData.PSKRAJSZER_LEF, 2);
        PSKRAJSZER2 = Math.Round(siemensOrderData.PSKRAJSZER2_REF, 2);
        PSKRAJDL = Math.Round(siemensOrderData.PSKRAJDL_TEF, 2);
        PSKRAJDL2 = Math.Round(siemensOrderData.PSKRAJDL2_DEF, 2);
        PLASKH = Math.Round(siemensOrderData.PLASKH_BBH, 2);
        PLASKS = Math.Round(siemensOrderData.PLASKS_BBT, 2);
        HWALC = Math.Round(siemensOrderData.HWALC_CBH, 2);
        SWALC = Math.Round(siemensOrderData.SWALC_CBT, 2);
        SZTPLN = Math.Round(siemensOrderData.SZTPLN_BBN, 2);
        SZPLL = Math.Round(siemensOrderData.SZPLL_CBN, 2);
        SZTKR = Math.Round(siemensOrderData.SZTKR_PCS, 2);
        TFT = siemensOrderData.TFT;
        THTH = Math.Round(siemensOrderData.THTH, 2);
        TFTF = Math.Round(siemensOrderData.TFTF, 2);
        TFD = siemensOrderData.TFD;
        TFDH = Math.Round(siemensOrderData.TFDH, 2);
        TFDF = Math.Round(siemensOrderData.TFDF, 2);
        TFL = siemensOrderData.TFL;
        TFLH = Math.Round(siemensOrderData.TFLH, 2);
        TFLF = Math.Round(siemensOrderData.TFLF, 2);
        TFR = siemensOrderData.TFR;
        TFRH = Math.Round(siemensOrderData.TFRH, 2);
        TFRF = Math.Round(siemensOrderData.TFRF, 2);
        NRZLEC = siemensOrderData.NRZLEC.ToString();
    }
}
