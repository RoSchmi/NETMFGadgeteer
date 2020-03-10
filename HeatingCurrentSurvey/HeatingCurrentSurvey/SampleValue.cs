using System;
using Microsoft.SPOT;

namespace HeatingSurvey
{
    public class SampleValue
    {
        string _partitionKey;
        DateTime _timeOfSample;
        int _timeOffsetUTC;
        double _sampleValue;
        double _dayMin;
        double _dayMax;
        double _T_0;
        byte _ID_0;
        UInt16 _H_0;
        bool _Lo_0;
        double _T_1;
        byte _ID_1;
        UInt16 _H_1;
        bool _Lo_1;
        double _T_2;
        byte _ID_2;
        UInt16 _H_2;
        bool _Lo_2;
        double _T_3;
        byte _ID_3;
        UInt16 _H_3;
        bool _Lo_3;
        double _T_4;
        byte _ID_4;
        UInt16 _H_4;
        bool _Lo_4;
        double _T_5;
        byte _ID_5;
        UInt16 _H_5;
        bool _Lo_5;
        double _T_6;
        byte _ID_6;
        UInt16 _H_6;
        bool _Lo_6;
        double _T_7;
        byte _ID_7;
        UInt16 _H_7;
        bool _Lo_7;
        string _secondReport;
        string _status;
        string _location;
        TimeSpan _timeFromLast;
        int _RSSI;
        int _iterations;
        uint _remainingRam;
        int _forcedReboots;
        int _badReboots;
        int _sendErrors;
        char _bootReason;
        bool _forceSend;
        string _message;

        public SampleValue(string pPartitionKey, DateTime pTimeOfSample, int pTimeOffsetUTC, double pSampleValue, double pDayMin, double pDayMax, double pT_0, byte pID_0, UInt16 pH_0, bool pLo_0, double pT_1, byte pID_1, UInt16 pH_1, bool pLo_1, double pT_2, byte pID_2, UInt16 pH_2, bool pLo_2, double pT_3, byte pID_3, UInt16 pH_3, bool pLo_3, double pT_4, byte pID_4, UInt16 pH_4, bool pLo_4, double pT_5, byte pID_5, UInt16 pH_5, bool pLo_5, double pT_6, byte pID_6, UInt16 pH_6, bool pLo_6, double pT_7, byte pID_7, UInt16 pH_7, bool pLo_7, string pSecondReport, string pStatus, string pLocation, TimeSpan ptimeFromLast, int pRSSI, int pIterations, uint pRemainingRam, int pforcedReboots, int pbadReboots, int pSendErrors, char pBootReason, bool pForceSend, string pMessage)
        {
            this._partitionKey = pPartitionKey;
            this._timeOfSample = pTimeOfSample;
            this._timeOffsetUTC = pTimeOffsetUTC;
            this._sampleValue = pSampleValue;
            this._dayMin = pDayMin;
            this._dayMax = pDayMax;
            this._T_0 = pT_0;
            this._T_1 = pT_1;
            this._T_2 = pT_2;
            this._T_3 = pT_3;
            this._T_4 = pT_4;
            this._T_5 = pT_5;
            this._T_6 = pT_6;
            this._T_7 = pT_7;
            this._ID_0 = pID_0;
            this._ID_1 = pID_1;
            this._ID_2 = pID_2;
            this._ID_3 = pID_3;
            this._ID_4 = pID_4;
            this._ID_5 = pID_5;
            this._ID_6 = pID_6;
            this._ID_7 = pID_7;
            this._H_0 = pH_0;
            this._H_1 = pH_1;
            this._H_2 = pH_2;
            this._H_3 = pH_3;
            this._H_4 = pH_4;
            this._H_5 = pH_5;
            this._H_6 = pH_6;
            this._H_7 = pH_7;

            this._Lo_0 = pLo_0;
            this._Lo_1 = pLo_1;
            this._Lo_2 = pLo_2;
            this._Lo_3 = pLo_3;
            this._Lo_4 = pLo_4;
            this._Lo_5 = pLo_5;
            this._Lo_6 = pLo_6;
            this._Lo_7 = pLo_7;

            this._secondReport = pSecondReport;
            this._status = pStatus;
            this._location = pLocation;
            this._timeFromLast = ptimeFromLast;
            this._RSSI = pRSSI;
            this._iterations = pIterations;
            this._remainingRam = pRemainingRam;
            this._forcedReboots = pforcedReboots;
            this._badReboots = pbadReboots;
            this._sendErrors = pSendErrors;
            this._bootReason = pBootReason;
            this._forceSend = pForceSend;
            this._message = pMessage;
            
        }

        public string PartitionKey
        {
            get { return this._partitionKey; }
            set { this._partitionKey = value; }
        }

        public DateTime TimeOfSample
        {
            get { return this._timeOfSample; }
            set { this._timeOfSample = value; }
        }
        public int TimeOffSetUTC
        {
            get { return this._timeOffsetUTC;}
            set { this._timeOffsetUTC = value; }
        }      
        public double TheSampleValue
        {
            get { return this._sampleValue; }
            set { this._sampleValue = value; }
        }
        public double DayMin
        {
            get { return this._dayMin; }
            set { this._dayMin = value; }
        }
        public double DayMax
        {
            get { return this._dayMax; }
            set { this._dayMax = value; }
        }
        public double T_0
        {
            get { return this._T_0; }
            set { this._T_0 = value; }
        }
        public double T_1
        {
            get { return this._T_1; }
            set { this._T_1 = value; }
        }
        public double T_2
        {
            get { return this._T_2; }
            set { this._T_2= value; }
        }
        public double T_3
        {
            get { return this._T_3; }
            set { this._T_3 = value; }
        }
        public double T_4
        {
            get { return this._T_4; }
            set { this._T_4 = value; }
        }
        public double T_5
        {
            get { return this._T_5; }
            set { this._T_5 = value; }
        }
        public double T_6
        {
            get { return this._T_6; }
            set { this._T_6 = value; }
        }
        public double T_7
        {
            get { return this._T_7; }
            set { this._T_7 = value; }
        }


        public byte ID_0
        {
            get { return this._ID_0; }
            set { this._ID_0 = value; }
        }
        public byte ID_1
        {
            get { return this._ID_1; }
            set { this._ID_1 = value; }
        }
        public byte ID_2
        {
            get { return this._ID_2; }
            set { this._ID_2 = value; }
        }
        public byte ID_3
        {
            get { return this._ID_3; }
            set { this._ID_3 = value; }
        }
        public byte ID_4
        {
            get { return this._ID_4; }
            set { this._ID_4 = value; }
        }
        public byte ID_5
        {
            get { return this._ID_5; }
            set { this._ID_5 = value; }
        }
        public byte ID_6
        {
            get { return this._ID_6; }
            set { this._ID_6 = value; }
        }
        public byte ID_7
        {
            get { return this._ID_7; }
            set { this._ID_7 = value; }
        }


        public UInt16 H_0
        {
            get { return this._H_0; }
            set { this._H_0 = value; }
        }
        public UInt16 H_1
        {
            get { return this._H_1; }
            set { this._H_1 = value; }
        }
        public UInt16 H_2
        {
            get { return this._H_2; }
            set { this._H_2 = value; }
        }
        public UInt16 H_3
        {
            get { return this._H_3; }
            set { this._H_3 = value; }
        }
        public UInt16 H_4
        {
            get { return this._H_4; }
            set { this._H_4 = value; }
        }
        public UInt16 H_5
        {
            get { return this._H_5; }
            set { this._H_5 = value; }
        }
        public UInt16 H_6
        {
            get { return this._H_6; }
            set { this._H_6 = value; }
        }
        public UInt16 H_7
        {
            get { return this._H_7; }
            set { this._H_7 = value; }
        }


        public bool Lo_0
        {
            get { return this._Lo_0; }
            set { this._Lo_0 = value; }
        }
        public bool Lo_1
        {
            get { return this._Lo_1; }
            set { this._Lo_1 = value; }
        }
        public bool Lo_2
        {
            get { return this._Lo_2; }
            set { this._Lo_2 = value; }
        }
        public bool Lo_3
        {
            get { return this._Lo_3; }
            set { this._Lo_3 = value; }
        }
        public bool Lo_4
        {
            get { return this._Lo_4; }
            set { this._Lo_4 = value; }
        }
        public bool Lo_5
        {
            get { return this._Lo_5; }
            set { this._Lo_5 = value; }
        }
        public bool Lo_6
        {
            get { return this._Lo_6; }
            set { this._Lo_6 = value; }
        }
        public bool Lo_7
        {
            get { return this._Lo_7; }
            set { this._Lo_7 = value; }
        }



        public string SecondReport
        {
            get { return this._secondReport; }
            set { this._secondReport = value; }
        }
        public string Status
        {
            get { return this._status; }
            set { this._status = value; }
        }
        public string Location
        {
            get { return this._location; }
            set { this._location = value; }
        }
        public TimeSpan TimeFromLast
        {
            get { return this._timeFromLast; }
            set { this._timeFromLast = value; }
        }
        public int RSSI
        {
            get { return this._RSSI; }
            set { this._RSSI = value; }
        }

        public int Iterations
        {
            get { return this._iterations; }
            set { this._iterations = value; }
        }
        public uint RemainingRam
        {
            get { return this._remainingRam; }
            set { this._remainingRam = value; }
        }
        public int ForcedReboots
        {
            get { return this._forcedReboots; }
            set { this._forcedReboots = value; }
        }
        public int BadReboots
        {
            get { return this._badReboots; }
            set { this._badReboots = value; }
        }
        public int SendErrors
        {
            get { return this._sendErrors; }
            set { this._sendErrors = value; }
        }
        public char BootReason
        {
            get { return this._bootReason; }
            set { this._bootReason = value; }
        }

        public bool ForceSend
        {
            get { return this._forceSend; }
            set { this._forceSend = value; }

        }

        public string Message
        {
            get { return this._message; }
            set { this._message = value; }
        }

    }
}
