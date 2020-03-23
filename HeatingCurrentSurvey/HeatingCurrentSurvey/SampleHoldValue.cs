using System;
using Microsoft.SPOT;

namespace HeatingSurvey
{
    class SampleHoldValue
    {
        public SampleHoldValue(ushort pTemp, ushort pHumid)
        {
            this.Temp = pTemp;
            this.Humid = pHumid;
        }
        public ushort Temp {get; set;}
        public ushort Humid { get; set; }
        
    }
}
