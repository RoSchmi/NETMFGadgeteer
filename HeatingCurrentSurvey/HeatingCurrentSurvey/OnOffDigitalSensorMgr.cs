using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;

namespace HeatingSurvey
{
    class OnOffDigitalSensorMgr : OnOffBaseSensorMgr
    {  
       

        private static InterruptPort input;      
        Thread ReadBurnerThread;

       


        public OnOffDigitalSensorMgr(GHI.Processor.DeviceType deviceType, int socketNumber, int pDstOffset, string pDstStart, string pDstEnd, string pSensorLabel = "undef", string pSensorLocation = "undef", string pMeasuredQuantity = "undef", string pDestinationTable = "undef", string pChannel = "000")
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

            
            input = new InterruptPort(deviceType == GHI.Processor.DeviceType.EMX ? GHI.Pins.FEZSpider.Socket4.Pin4 : GHI.Pins.FEZSpiderII.Socket4.Pin3, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeBoth);
                      
            input.ClearInterrupt();           
            input.DisableInterrupt();
            _stopped = true;
            ReadBurnerThread = new Thread(runReadBurnerThread);
            ReadBurnerThread.Start();
        }

       
         
        public void Start()
        {
            oldState = input.Read() ? InputSensorState.Low : InputSensorState.High;          
            _stopped = false;        
        }

        public void Stop()
        {
            
            oldState = input.Read() ? InputSensorState.Low : InputSensorState.High;
            
            _stopped = true;                   
        }

        private void runReadBurnerThread()
        {
            DateTime actTime = DateTime.Now;
            oldState = input.Read() ? InputSensorState.Low : InputSensorState.High;
            

            while (true)
            {
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };
                if (!_stopped)
                {                  
                    if (input.Read() == false)    // input = low, burner is on
                                       //if (input.Read() < 0.5)
                    {
                        Thread.Sleep(20);         // debouncing
                        if (input.Read() == false)
                                      //if (input.Read() < 0.5)
                        {                           
                            if (oldState == InputSensorState.High)
                            {
                                actState = InputSensorState.Low;                             
                                OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, 0x00, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                                oldState = InputSensorState.Low;
                            }
                        }
                    }
                    else                         // input = high, burner is off
                    {
                        Thread.Sleep(20);             // (debouncing)
                        if (input.Read() == true)    // input still high 
                                           //if (input.Read() > 0.5)
                        {                           
                            if (oldState == InputSensorState.Low)
                            {
                                actState = InputSensorState.High;                               
                                OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, 0x00, DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)), SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, false));
                                oldState = InputSensorState.High;
                            }
                        }
                    }
                    actTime = DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true));
                    // Send an input high event (means burner is off) in the last 30 seconds of each day and wait on the next day
                    if (actTime.Hour == 23 && actTime.Minute == 59 && actTime.Second > 30)
                    {
                        actState = InputSensorState.High;

                        // RoSchmi
                        //OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, 0x00, DateTime.Now, SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, true));
                        OnDigitalOnOffSensorSend(this, new OnOffSensorEventArgs(actState, oldState, 0x00, actTime, SensorLabel, SensorLocation, MeasuredQuantity, DestinationTable, Channel, true));

                        oldState = InputSensorState.High;
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        // wait on the next day
                        while (actTime.Day == DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)).Day)
                        {
                            Thread.Sleep(100);
                        }
                    }
                }
                Thread.Sleep(200);   // Read Sensor every 200 ms
            }
        }

       

        #region Delegate
        /// <summary>
        /// The delegate that is used to handle the data message.
        /// </summary>
        /// <param name="sender">The <see cref="ReadBurnerThread"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>

        //public delegate void burnerSensorEventhandler(BurnerSensorMgr sender, BurnerSensorEventArgs e);
          //public delegate void burnerSensorEventhandler(OnOffDigitalSensorMgr sender, OnOffSensorEventArgs e);
          public delegate void digitalOnOffSensorEventhandler(OnOffDigitalSensorMgr sender, OnOffSensorEventArgs e);

        /// <summary>
        /// Raised when a message from the PRU is received
        /// </summary>
        public event digitalOnOffSensorEventhandler digitalOnOffSensorSend;

        //private burnerSensorEventhandler onBurnerSensorSend;
        private digitalOnOffSensorEventhandler onDigitalOnOffSensorSend;

        //private void OnBurnerSensorSend(BurnerSensorMgr sender, BurnerSensorEventArgs e)
        private void OnDigitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffSensorEventArgs e)
        {
            if (this.onDigitalOnOffSensorSend == null)
            {
                this.onDigitalOnOffSensorSend = this.OnDigitalOnOffSensorSend;
            }
            this.digitalOnOffSensorSend(sender, e);
        }
        #endregion
    }
}
