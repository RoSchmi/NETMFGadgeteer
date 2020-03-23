using System;
using Microsoft.SPOT;

namespace HeatingSurvey
{
    public class SensorValue
    {
        /*
        DateTime _lastNetmfTime;
        byte _channel;
        byte _sensorID;
        UInt32 _sampleTime;
        UInt16 _temp;
        double _tempDouble;
        UInt16 _hum;
        byte _randomId;
        bool _batteryIsLow;
        */

        public SensorValue(DateTime pLastNetmfTime, byte pChannel, byte pSensorID, UInt32 pSampleTime, UInt16 pTemp, double pTempDouble, UInt16 pHum, byte pRandomId, bool pBatteryIsLow)
        {
            /*
            this._lastNetmfTime = pLastNetmfTime;
            this._channel = pChannel;
            this._sensorID = pSensorID;
            this._sampleTime = pSampleTime;
            this._temp = pTemp;
            this._tempDouble = pTempDouble;
            this._hum = pHum;
            this._randomId = pRandomId;
            this._batteryIsLow = pBatteryIsLow;
            */
            this.LastNetmfTime = pLastNetmfTime;
            this.Channel = pChannel;
            this.SensorId = pSensorID;
            this.SampleTime = pSampleTime;
            this.Temp = pTemp;
            this.TempDouble = pTempDouble;
            this.Hum = pHum;
            this.RandomId = pRandomId;
            this.BatteryIsLow = pBatteryIsLow;


        }
        public DateTime LastNetmfTime { get; set; }
        public byte Channel { get; set; }
        public byte SensorId { get; set; }
        public UInt32 SampleTime { get; set; }
        public UInt16 Temp { get; set; }
        public double TempDouble { get; set; }
        public UInt16 Hum { get; set; }
        public byte RandomId { get; set; }
        public bool BatteryIsLow { get; set; }


        /*
        public DateTime LastNetmfTime
        {
            get { return this._lastNetmfTime; }
            set { this._lastNetmfTime = value; }
        }

        public byte Channel
        {
            get { return this._channel; }
            set { this._channel = value; }
        }
        public byte SensorId
        {
            get { return this._sensorID; }
            set { this._sensorID = value; }
        }
        public UInt32 SampleTime
        {
            get { return this._sampleTime; }
            set { this._sampleTime = value; }
        }
        public UInt16 Temp
        {
            get { return this._temp; }
            set { this._temp = value; }
        }
        public double TempDouble
        {
            get { return this._tempDouble; }
            set { this._tempDouble = value; }
        }
        public UInt16 Hum
        {
            get { return this._hum; }
            set { this._hum = value; }
        }
        public byte RandomId
        {
            get { return this._randomId; }
            set { this._randomId = value; }
        }
        public bool BatteryIsLow
        {
            get { return this._batteryIsLow; }
            set { this._batteryIsLow = value; }
        }
         */
    }
}
