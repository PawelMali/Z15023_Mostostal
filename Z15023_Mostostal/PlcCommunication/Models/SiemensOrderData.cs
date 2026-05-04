using System;
using System.Runtime.InteropServices;
using AutomatedSolutions.ASCommStd.SI.S7.Data;

namespace Z25023_Mostostal.PlcCommunication.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public class SiemensOrderData : UDT
    {
        public SiemensString KOLZLEC = new SiemensString(); //Nr. Zlecenia
        public SiemensString CZESC = new SiemensString();     //Numer częsci zlecenia
        public Int16 PRZESYLKA; //Przesyłka dotycząca paletyzacji
        public Int16 FAKTOR;    //Krotnośc przesyłki -  ilość sztuk paletyzacji
        public Single SZTUKPOZ;  //Ilość krat na przesyłkę
        public Single POZ_WYKWYS;    //LP.
        public SiemensString TYP = new SiemensString(); //Typ kraty
        public Single DLUGOSC;   //Długość kraty
        public Single SZEROKOSC; //Szerokość kraty
        public Single DRZECZ_BBL;   //Długość płaskownika nośnego - BBL
        public Single SZRZECZ_CBL;  //Długość płaskownika Łączącego - CBL
        public Single OCZKOH_BBS;   //Podziałka pomiędzy płaskownikami nośnymi - BBS
        public Single OCZKOL_CBS;   //Podziałka pomiędzy płaskownikami łączącymi - CBS
        public Single PSKRAJSZER_LEF;   //Pole skrajne lewe łączący - LEF
        public Single PSKRAJSZER2_REF;  //Pole skrajne prawe łączący - REF
        public Single PSKRAJDL_TEF;     //Pole skrajne górne nośny - TEF
        public Single PSKRAJDL2_DEF;    //Pole skrajne dolne nosny - DEF
        public Single PLASKH_BBH;   //Wysokość płaskownika nośnego - BBH
        public Single PLASKS_BBT;   //Grubość płaskownika noścnego - BBT
        public Single HWALC_CBH;    //Wysokość płaskownika łączącego - CBH
        public Single SWALC_CBT;    //Grubość płaskownika łączącego - CBT
        public Single SZTPLN_BBN;    //Ilość płaskowników nośnych - BBN
        public Single SZPLL_CBN;     //Ilość płaskowników łaczących - CBN
        public Single SZTKR_PCS;     //Sztuk krat do wykonania - PCS
        public Int16 TFT;   //Typ obramowania góra - TFT
        public Single THTH; //Wysokość obramowania góra - THTH
        public Single TFTF; //Grubość obramowania góra - TFTF
        public Int16 TFD;   //Typ obramowania dół - TFD
        public Single TFDH; //Wysokość obramowania dół - TFDH
        public Single TFDF; //Grubość obramowania dół - TFDF
        public Int16 TFL;   //Typ obramowania lewo - TFL
        public Single TFLH; //Wysokość obramowania lewo - TFLH
        public Single TFLF; //Grubość obramowania lewo - TFLF
        public Int16 TFR;   //Typ obramowania prawo - TFR
        public Single TFRH; //Wysokość obramowania prawo - TFRH 
        public Single TFRF; //Grubość obramowania prawo - TFRF
        public SiemensString NRZLEC = new SiemensString();  //Numer zlecenia ??

    }
}
