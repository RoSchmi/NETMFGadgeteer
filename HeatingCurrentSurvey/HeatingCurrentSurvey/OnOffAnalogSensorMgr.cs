using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;

namespace HeatingSurvey
{
    class OnOffAnalogSensorMgr : OnOffBaseSensorMgr
    {        
        private static readonly object theLock = new object();
                  
        private int threshold = 10;
        private static AnalogInput input;
       

        Thread ReadSensorThread;     

        public OnOffAnalogSensorMgr(GHI.Processor.DeviceType deviceType, int socketNumber, int pThreshold, int pDstOffset, string pDstStart, string pDstEnd, string pSensorLabel = "undef", string pSensorLocation = "undef", string pMeasuredQuantity = "undef", string pDestinationTable = "undef", string pChannel = "000")
        {
            if ((deviceType != GHI.Processor.DeviceType.EMX) && (deviceType != GHI.Processor.DeviceType.G120E))
            {
                throw new NotSupportedException("Mainboard is not supported");
            }
            dstOffset = pDstOffset;
            dstStart = pDstStart;
            dstEnd = pDstEnd;

            SensorLabel = pSensorLabel;
            SensorLocation = pSensorLocation;
            MeasuredQuantity = pMeasuredQuantity;
            DestinationTable = pDestinationTable;
            Channel = pChannel;
          
            input = new AnalogInput(deviceType == GHI.Processor.DeviceType.EMX ? GHI.Pins.FEZSpider.Socket9.AnalogInput4 : GHI.Pins.FEZSpiderII.Socket9.AnalogInput4);
            
            _stopped = true;
            ReadSensorThread = new Thread(runReadSensorThread);
            ReadSensorThread.Start();
            threshold = pThreshold;
        }
        

        void runReadSensorThread()
        {

            DateTime actTime = DateTime.Now;
            int counter;
            int sum = 0;
            
            for (counter = 0; counter < 100; counter++)
            {
                sum += input.ReadRaw();
            }
            oldState = sum > threshold ? InputSensorState.Low : InputSensorState.High;
            sum = 0;

            while (true)
            {
                if (!_stopped)
                {
                    /*
                    // This was for tests
                    if (oldState == InputSensorState.High)
                    {
                        actState = InputSensorState.Low;
                        OnCurrentSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                        oldState = InputSensorState.Low;
                    }
                    else
                    {
                        actState = InputSensorState.High;
                        OnCurrentSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                        oldState = InputSensorState.High;
                    }
                    Thread.Sleep(20000);
                    */
                    
                    lock (theLock)
                    {
                        sum = 0;
                        for (counter = 0; counter < 100; counter++)
                        {
                            sum += input.ReadRaw();
                        }
                        if (sum > threshold)  // pump is on
                        {
                            Thread.Sleep(20);             // (debouncing)
                            sum = 0;
                            for (counter = 0; counter < 100; counter++)
                            {
                                sum += input.ReadRaw();
                            }
                            if (sum > threshold)  // pump is still on
                            {
                                if (oldState == InputSensorState.High)
                                {
                                    actState = InputSensorState.Low;
                                    //DateTime time = DateTime.Now;
                                    //Debug.Print("On " + time.TimeOfDay  + ": " + sum + "\r\n");
                                    //OnCurrentSensorSend(this, new CurrentSensorEventArgs(actState, oldState, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                                    OnCurrentSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                                    oldState = InputSensorState.Low;
                                }
                            }
                        }
                        else
                        {

                            Thread.Sleep(20);             // (debouncing)
                            sum = 0;
                            for (counter = 0; counter < 100; counter++)
                            {
                                sum += input.ReadRaw();
                            }
                            if (sum <= threshold)  // pump is still off
                            {
                                if (oldState == InputSensorState.Low)
                                {
                                    actState = InputSensorState.High;
                                    //DateTime time = DateTime.Now;
                                    //Debug.Print("Off " + time.TimeOfDay  + ": " + sum + "\r\n");
                                    //OnCurrentSensorSend(this, new CurrentSensorEventArgs(actState, oldState, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                                    OnCurrentSensorSend(this, new OnOffSensorEventArgs(actState, oldState, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                                    oldState = InputSensorState.High;
                                }
                            }

                        }
                    }
                    
                }
                Thread.Sleep(202);
            }
        }

        public void Start()
        {
            int counter;
            int sum = 0;
            for (counter = 0; counter < 100; counter++)
            {
                sum += input.ReadRaw();
            }
            oldState = sum > threshold ? InputSensorState.Low : InputSensorState.High;          
            _stopped = false;
        }

        public void Stop()
        {
            int counter;
            int sum = 0;
            for (counter = 0; counter < 100; counter++)
            {
                sum += input.ReadRaw();
            }
            oldState = sum > threshold ? InputSensorState.Low : InputSensorState.High;                
            _stopped = true;         
        }

        public InputSensorState ReadState()
        {
            lock (theLock)
            {
                return oldState;
            }

        }

        #region Delegate
        /// <summary>
        /// The delegate that is used to handle the data message.
        /// </summary>
        /// <param name="sender">The <see cref="ReadCurrentThread"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>

        //public delegate void currentSensorEventhandler(BoilerSensorMgr sender, CurrentSensorEventArgs e);
        public delegate void currentSensorEventhandler(OnOffAnalogSensorMgr sender, OnOffSensorEventArgs e);

        /// <summary>
        /// Raised when a message from the PRU is received
        /// </summary>
        public event currentSensorEventhandler currentSensorSend;

        private currentSensorEventhandler onCurrentSensorSend;

        //private void OnCurrentSensorSend(BoilerSensorMgr sender, CurrentSensorEventArgs e)
        private void OnCurrentSensorSend(OnOffAnalogSensorMgr sender, OnOffSensorEventArgs e)
        {
            if (this.onCurrentSensorSend == null)
            {
                this.onCurrentSensorSend = this.OnCurrentSensorSend;
            }
            this.currentSensorSend(sender, e);
        }
        #endregion
    }
}

