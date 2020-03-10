using System;
using Microsoft.SPOT;

namespace HeatingSurvey
{
    class SampleHoldValue
    {
        ushort _temp;
        ushort _humid;

        public SampleHoldValue(ushort pTemp, ushort pHumid)
        {
            this._temp = pTemp;
            this._humid = pHumid;
        }
        public ushort Temp
        {
            get { return this._temp; }
            set { this._temp = value; }
        }
         public ushort Humid
        {
            get { return this._humid; }
            set { this._humid = value; }
        }
    }
}
