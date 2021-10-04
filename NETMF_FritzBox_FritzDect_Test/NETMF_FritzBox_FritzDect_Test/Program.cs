

using System;
using System.Threading;
using System.Net;
using System.Text;
//using System.Text.RegularExpressions;
using Microsoft.SPOT;
using Microsoft.SPOT.IO;
using Microsoft.SPOT.Hardware;
using GHI.IO.Storage;
using System.IO;
using Microsoft.SPOT.Time;
using System.Xml;
using System.Ext.Xml;
using System.IO.Ports;
using System.Security.Cryptography.X509Certificates;
using System.Collections;
using Microsoft.SPOT.Net.NetworkInformation;
using GHI.Networking;
using GHI.Processor;
using GHI.Pins;
//using HeatingSurvey;
using RoSchmi.Net.Azure.Storage;
using RoSchmi.Net;
//using RoSchmi.ButtonNETMF;
using RoSchmi.DayLihtSavingTime;
//using RoSchmi.RFM69_NETMF;
//using RoSchmi.Utilities;
//using PervasiveDigital;
//using PervasiveDigital.Json;

using RoSchmi.Net.Fritzbox;



namespace NETMF_FritzBox_FritzDect_Test
{


    public class Program
    {
        #region Settings that have to be set by the user
        //************ Settings: These parameters have to be set by the user  *******************

        #region Common Settings (time offset to GMT, Interval of Azure Sends, Debugging parameters and things concerning fiddler attachment
        // Common Settings

        // Select Mainboard (here FEZ Spider)
        private const GHI.Processor.DeviceType _deviceType = GHI.Processor.DeviceType.EMX;

        // Select if Static IP or DHCP is used (can be overwritten by parameters in an XML-File on the SD-Card (file Temp_Survey.xml)
        // When there is no such file on the SD-Card it is created with the parameters in the program
        private static bool useDHCP = true;

        #region Region IP-Addresses for static IP
        // Static Network IP-Addresses
        private static string DeviceIpAddress = "192.168.1.66";
        //private static string DeviceIpAddress = "192.168.2.66";
        private static string GateWayIpAddress = "192.168.1.1";
        //private static string GateWayIpAddress = "192.168.2.1";
        private static string SubnetMask = "255.255.255.0";
        private static string DnsServerIpAddress = "192.168.1.1";
        //private static string DnsServerIpAddress = "192.168.2.1";
        #endregion

        private static string TimeServer_1 = "time1.google.com";
        private static string TimeServer_2 = "1.pool.ntp.org";

        //private static string TimeServer_1 = "fritz.box";

        //private static int timeZoneOffset = -720;
        //private static int timeZoneOffset = -715;
        //private static int timeZoneOffset = -500;
        //private static int timeZoneOffset = -300;     // New York offest in minutes of your timezone to Greenwich Mean Time (GMT)
        //private static int timeZoneOffset = -60;
        //private static int timeZoneOffset = 0;       // Lissabon offest in minutes of your timezone to Greenwich Mean Time (GMT)
        private static int timeZoneOffset = 60;      // Berlin offest in minutes of your timezone to Greenwich Mean Time (GMT)
        //private static int timeZoneOffset = 120;
        //private static int timeZoneOffset = 180;     // Moskau offest in minutes of your timezone to Greenwich Mean Time (GMT) 
        //private static int timeZoneOffset = 240;
        // private static int timeZoneOffset = 243;
        //private static int timeZoneOffset = 680;
        //private static int timeZoneOffset = 720;


        // Europe                                           //DayLightSavingTimeSettings
        private static int dstOffset = 60; // 1 hour (Europe 2016)
        private static string dstStart = "Mar lastSun @2";
        private static string dstEnd = "Oct lastSun @3";

        #endregion

        //  USA
        /*
        private static int dstOffset = 60; // 1 hour (US 2013)
        private static string dstStart = "Mar Sun>=8"; // 2nd Sunday March (US 2013)
        private static string dstEnd = "Nov Sun>=1"; // 1st Sunday Nov (US 2013)
        */

        // if time has elapsed, the acutal entry in the SampleValueBuffer is sent to azure, otherwise it is neglected (here: 1 sec, so it is always sended)
        private static TimeSpan sendInterval_Burner = new TimeSpan(0, 0, 1); // If this time interval has expired since the last sending to azure,         
        private static TimeSpan sendInterval_Boiler = new TimeSpan(0, 0, 1);
        private static TimeSpan sendInterval_Solar = new TimeSpan(0, 0, 1);
        private static TimeSpan sendInterval_Current = new TimeSpan(0, 0, 1);
        private static TimeSpan sendInterval_SolarTemps = new TimeSpan(0, 0, 1);

        // RoSchmi
        //private static bool workWithWatchDog = true;    // Choose whether the App runs with WatchDog, should normally be set to true
        private static bool workWithWatchDog = false; 
        private static int watchDogTimeOut = 50;        // WatchDog timeout in sec: Max Value for G400 15 sec, G120 134 sec, EMX 4.294 sec
        // = 50 sec, don't change without need, may not be below 30 sec     

        // If the free ram of the mainboard is below this level it will reboot (because of https memory leak)
        private static int freeRamThreshold = 4300000;
        //private static int freeRamThreshold = 3300000;

        // You can select what kind of Debug.Print messages are sent

        //public static AzureStorageHelper.DebugMode _AzureDebugMode = AzureStorageHelper.DebugMode.StandardDebug;
        //public static AzureStorageHelper.DebugMode _AzureDebugMode = AzureStorageHelper.DebugMode.NoDebug;
        //public static AzureStorageHelper.DebugLevel _AzureDebugLevel = AzureStorageHelper.DebugLevel.DebugAll;

        // To use Fiddler as WebProxy set attachFiddler = true and set the proper IPAddress and port
        // Use the local IP-Address of the PC where Fiddler is running
        // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
        private static bool attachFiddler = false;
        private const string fiddlerIPAddress = "192.168.1.21"; // Set to the IP-Adress of your PC
        private const int fiddlerPort = 8888;                   // Standard port of fiddler

        // End of Common Settings
        #endregion

        #region Fields

        static AutoResetEvent waitForCurrentCallback = new AutoResetEvent(false);
        static AutoResetEvent waitForSolarTempsCallback = new AutoResetEvent(false);

        public static SDCard SD;
        private static bool _fs_ready = false;

        //public static ButtonNETMF myButton;
        //public static ButtonNETMF LDR1Button;

        private static OutputPort _Led;

        //private static GHI.Networking.EthernetENC28J60 netif;
        private static GHI.Networking.EthernetBuiltIn netif;

        private static bool _hasAddress;
        private static bool _available;

        // The watchdog is activated in the first _sensorControlTimer_Tick event
        private static bool watchDogIsAcitvated = false;// Don't change, choosing is done in the workWithWatchDog variable

        static Thread WatchDogCounterResetThread;

        //private static Counters _counters = new Counters();

        private static int _azureSends = 1;
        private static int _forcedReboots = 0;
        private static int _badReboots = 0;
        private static int _azureSendErrors = 0;

        private static bool _willRebootAfterNextEvent = false;

        private const double InValidValue = 999.9;

        // RoSchmi
        static TimeSpan makeInvalidTimeSpan = new TimeSpan(2, 15, 0);  // When this timespan has elapsed, old sensor values are set to invalid


        private static string _lastResetCause;
        //private static DateTime _timeOfLastSensorEvent;

        private static readonly object MainThreadLock = new object();



        // Regex ^: Begin at start of line; [a-zA-Z0-9]: these chars are allowed; [^<>]: these chars ar not allowd; +$: test for every char in string until end of line
        // Is used to exclude some not allowed characters in the strings for the name of the Azure table and the message entity property
        //static Regex _tableRegex = new Regex(@"^[a-zA-Z0-9]+$");
        //static Regex _stringRegex = new Regex(@"^[^<>]+$");

        private static int _azureSendThreads = 0;
        private static int _azureSendThreadResetCounter = 0;


        // Certificate of Azure, included as a Resource
        //static byte[] caAzure = Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root);

        // See -https://blog.devmobile.co.nz/2013/03/01/https-with-netmf-http-client-managing-certificates/ how to include a certificate

        private static X509Certificate[] caCerts;

        private static IPAddress localIpAddress = null;
        private Microsoft.SPOT.Net.NetworkInformation.NetworkInterface settings;

        private static TimeServiceSettings timeSettings;
        private static bool timeServiceIsRunning = false;
        private static bool timeIsSet = false;

        /*
        private static OnOffDigitalSensorMgr myBurnerSensor;
        private static OnOffAnalogSensorMgr myStoragePumpSensor;
        private static OnOffRfm69SensorMgr mySolarPumpCurrentSensor;
        private static CloudStorageAccount myCloudStorageAccount;
        private static AzureSendManager_Burner myAzureSendManager_Burner;
        private static AzureSendManager_Boiler myAzureSendManager_Boiler;
        private static AzureSendManager_Solar myAzureSendManager_Solar;
        private static AzureSendManager myAzureSendManager;
        private static AzureSendManager_SolarTemps myAzureSendManager_SolarTemps;


        string lastOutString = string.Empty;

        static SensorValue[] _sensorValueArr = new SensorValue[8];
        static SensorValue[] _sensorValueArr_last_1 = new SensorValue[8];
        static SensorValue[] _sensorValueArr_last_2 = new SensorValue[8];

        static SensorValue[] _sensorValueArr_Out = new SensorValue[8];


        static SampleHoldValue[] _samplHoldValues = new SampleHoldValue[8];   // To hold the last value for a time when there is a temp discordant value
        */


        static string user = "";
        static string password = "";
        static string fritzUrl = "fritz.box";
        static bool useHttps = false;


        const string FRITZ_DEVICE_AIN_01 = "226580442945";    // Ain of Fritz!Dect switchable power socket

       static NETMF_FritzAPI fritz = new NETMF_FritzAPI(user, password, fritzUrl, useHttps);

        #endregion

        public static void Main()
        {
            Debug.Print(Resources.GetString(Resources.StringResources.String1));

            // Debug.Print(Resources.GetString(Resources.StringResources.String1));

            #region Save last Reset Cause (Watchdog or Power/Reset)
            _lastResetCause = " PowerOrReset";

            if (GHI.Processor.Watchdog.LastResetCause == GHI.Processor.Watchdog.ResetCause.Watchdog)
            {
                _lastResetCause = " Watchdog";
                //Debug.Print("Last Reset Cause: Watchdog");
            }
            else
            {
                //Debug.Print("Last Reset Cause: Power/Reset");
            }
            if (GHI.Processor.Watchdog.Enabled)
            {
#if DebugPrint
                Debug.Print("Watchdog disabled");
#endif
                GHI.Processor.Watchdog.Disable();
            }
            #endregion

            #region Try to open SD-Card, if there is no SD-Card, doesn't matter
            try
            {
                SD = new SDCard();
                AutoResetEvent waitForSD = new AutoResetEvent(false);
                SD.Mount();
                RemovableMedia.Insert += (a, b) =>
                {
                    var theA = a;
                    var theB = b;
                    _fs_ready = true;
                    waitForSD.Reset();
                };

                waitForSD.WaitOne(1000, true);
            }
            catch (Exception ex1)
            {
#if DebugPrint
                Debug.Print("\r\nSD-Card not mounted! " + ex1.Message);
#endif
            }
            #endregion

            #region Try to get Network parameters from SD-Card, if SD-Card or no file Temp_Survey.xml --> take Program values
            if (_fs_ready)
            {
                try
                {
                    if (VolumeInfo.GetVolumes()[0].IsFormatted)
                    {
                        string rootDirectory = VolumeInfo.GetVolumes()[0].RootDirectory;

                        #region If file Temp_Survey.xml does not exist, write XML file to SD-Card
                        if (!File.Exists(rootDirectory + @"\Temp_Survey.xml"))
                        {
                            using (FileStream FileHandleWrite = new FileStream(rootDirectory + @"\Temp_Survey.xml", FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                using (XmlWriter xmlwrite = XmlWriter.Create(FileHandleWrite))
                                {
                                    xmlwrite.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\"");
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteComment("Contents of this XML file defines the network settings of the device");
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteStartElement("networksettings"); //root element
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteElementString("dhcp", "false");
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteElementString("ipaddress", "192.168.1.66");
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteElementString("gateway", "192.168.1.1");
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteElementString("dnsserver", "192.168.1.1");
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteEndElement();
                                    xmlwrite.WriteRaw("\r\n");
                                    xmlwrite.WriteComment("End");
                                    xmlwrite.Flush();
                                    xmlwrite.Close();
                                }
                            }
                            FinalizeVolumes();
                        }
                        #endregion

                        #region Read contents of file Temp_Survey.xml from SD-Card
                        FileStream FileHandleRead = new FileStream(rootDirectory + @"\Temp_Survey.xml", FileMode.Open);
                        XmlReaderSettings ss = new XmlReaderSettings();
                        ss.IgnoreWhitespace = true;
                        ss.IgnoreComments = false;
                        XmlReader xmlr = XmlReader.Create(FileHandleRead, ss);
                        string actElement = string.Empty;
                        while (!xmlr.EOF)
                        {
                            xmlr.Read();
                            switch (xmlr.NodeType)
                            {
                                case XmlNodeType.Element:
                                    //Debug.Print("element: " + xmlr.Name);
                                    actElement = xmlr.Name;
                                    break;
                                case XmlNodeType.Text:
                                    //Debug.Print("text: " + xmlr.Value);
                                    switch (actElement)
                                    {
                                        case "dhcp":
                                            if (xmlr.Value == "true")
                                            {
                                                useDHCP = true;
                                            }
                                            else
                                            {
                                                useDHCP = false;
                                            }
                                            break;
                                        case "ipaddress":
                                            DeviceIpAddress = xmlr.Value;
                                            break;
                                        case "gateway":
                                            GateWayIpAddress = xmlr.Value;
                                            break;
                                        case "dnsserver":
                                            GateWayIpAddress = xmlr.Value;
                                            break;
                                        default:
                                            break;
                                    }
                                    break;

                                default:
                                    //Debug.Print(xmlr.NodeType.ToString());
                                    break;
                            }
                        }
                        #endregion

                        SD.Unmount();
                    }
                    else
                    {
#if DebugPrint
                        Debug.Print("Storage is not formatted. " + "Format on PC with FAT32/FAT16 first!");
#endif
                    }
                    try
                    {
                        SD.Unmount();
                    }
                    catch { };
                }
                catch (Exception ex)
                {
#if DebugPrint
                    Debug.Print("SD-Card not opened! " + ex.Message);
#endif
                }
            }
            #endregion

            #region Initialize Network
            timeServiceIsRunning = false;
            FixedTimeService.SystemTimeChanged += FixedTimeService_SystemTimeChanged;
            FixedTimeService.SystemTimeChecked += FixedTimeService_SystemTimeChecked;


            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            //For Cobra III and ethernet ENC28 module on GXP Gadgeteer Bridge Socket 1 (SU)
            //netif = new GHI.Networking.EthernetENC28J60(Microsoft.SPOT.Hardware.SPI.SPI_module.SPI2, G120.P0_5, G120.P0_4, G120.P4_28);

            // for Spider and Spider II
            netif = new GHI.Networking.EthernetBuiltIn();

            // Not needed for Buildin Ethernet, is used to get a valid MAC Address vor Ethernet ENC28 Module
            // var myMac = GenerateUniqueMacAddr.GenerateUniqueMacAddress(myAzureAccount);

            // Alternatively to using myAzureAccount as the base to generate a MAC Address the unique ID of the G120
            // processor can be used (never tried this)
            // https://www.ghielectronics.com/community/forum/topic?id=12101

            // For STM32 processors this code can be used to read the unique ID
            // var theDeviceType = GHI.Processor.DeviceType.G120;
            // var deviceid = new byte[12];
            // GHI.Processor.AddressSpace.Read((uint)0x1FFF7A10, deviceid, 0, 12);

            /*
            // This must be commented out for Spider and Spider II BuildIn Ethernet
            if (!GenerateUniqueMacAddr.ByteArrayEquals(netif.PhysicalAddress, myMac))
            {
                //update the Mac Address
                netif.PhysicalAddress = myMac;
                //Hard Reboot the device so the newly Updated MAC is taking into consideration.
                PowerState.RebootDevice(false);
            }
            */
            netif.Open();


            if (useDHCP)
            {
                // for DHCP 
                netif.EnableDhcp();
                netif.EnableDynamicDns();
                while (netif.IPAddress == "0.0.0.0")
                {
#if DebugPrint
                    Debug.Print("Wait DHCP");
#endif
                    Thread.Sleep(300);
                }
                _hasAddress = true;
                Debug.Print("MAC is: " + netif.PhysicalAddress.ToHexString());
                Debug.Print("IP is: " + netif.IPAddress);
            }
            else
            {
                // for static IP
                netif.EnableStaticIP(DeviceIpAddress, SubnetMask, GateWayIpAddress);
                netif.EnableStaticDns(new[] { DnsServerIpAddress });
                while (!_hasAddress || !_available)
                {
                    Debug.Print("Wait static IP");
                    Thread.Sleep(100);
                }
            }

            #endregion


           
            if (fritz.init())
            {
                Debug.Print("Initialization for FritzBox is done");
            }
            else
            {
                Debug.Print("Initialization for FritzBox failed");
                while (true)
                {
                    Thread.Sleep(100);
                }
            }

            bool state_01 = true;
            while (true)
            {

                // Set every command in Try Catch block, if something went wrong, null is returned
                state_01 = fritz.getSwitchState(FRITZ_DEVICE_AIN_01) == "1";
                Debug.Print("Actually switch is  " + (state_01 ? "on" : "off"));

                
                try
                {                   
                    double thePower = double.Parse(fritz.getSwitchPower(FRITZ_DEVICE_AIN_01)) / 1000;
                    Debug.Print(thePower.ToString("F2"));
                }
                catch
                {
                    Debug.Print("Error: Power couldn't be read from Fritz!Dect");
                }

               
                
                double theEnergy = double.Parse(fritz.getSwitchEnergy(FRITZ_DEVICE_AIN_01));
                Debug.Print(theEnergy.ToString());

                double theTemperature = double.Parse(fritz.getTemperature(FRITZ_DEVICE_AIN_01)) / 10;
                Debug.Print(theTemperature.ToString() + " °C");

                Debug.Print(fritz.testSID());

                Debug.Print(fritz.getSwitchName(FRITZ_DEVICE_AIN_01));

                Debug.Print(fritz.getSwitchPresent(FRITZ_DEVICE_AIN_01) == "1" ? "Device is present" : "Device is not present");

                

                state_01 = fritz.setSwitchToggle(FRITZ_DEVICE_AIN_01) == "1";

                             
                Thread.Sleep(5000);
            }
            
        }



        #region NetworkAddressChanged
        static void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            Debug.Print("The network address has changed.");
            _hasAddress = netif.IPAddress != "0.0.0.0";
            Debug.Print("IP is: " + netif.IPAddress);

            if (!timeIsSet)
            {
                if (DateTime.Now < new DateTime(2016, 7, 1))
                {
                    Debug.Print("Going to set the time in NetworkAddressChanged event");
                    SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
                    Thread.Sleep(200);
                }
            }
            Thread.Sleep(20);

            Debug.Print("Time is: " + DateTime.Now.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true)).ToString());
        }
        #endregion

        #region NetworkAvailabilityChanged
        static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            //Debug.Print("Network available: " + e.IsAvailable.ToString());

            _available = e.IsAvailable;
        }
        #endregion

        #region Event button pressed  (LDR1 or other Button)
        /*
        static void myButton_ButtonPressed(ButtonNETMF sender, ButtonNETMF.ButtonState state)
        {
            Debug.Print("Button was pressed");
        }
        */
        #endregion

        #region myButton_ButtonReleased
        /*
        static void myButton_ButtonReleased(ButtonNETMF sender, ButtonNETMF.ButtonState state)
        {
            Debug.Print("Button was released");
        }
        */
        #endregion

        #region Method Finalize Volumes
        public static void FinalizeVolumes()
        {
            VolumeInfo[] vi = VolumeInfo.GetVolumes();
            for (int i = 0; i < vi.Length; i++)
                vi[i].FlushAll();
        }
        #endregion

        #region Method Activate Watchdog if not yet running
        public static void activateWatchdogIfAllowedAndNotYetRunning()
        {
            if (!watchDogIsAcitvated)
            {
                if (workWithWatchDog)
                {
                    Debug.Print("Watchdog Thread started!");
                    watchDogIsAcitvated = true;
                    GHI.Processor.Watchdog.Enable(watchDogTimeOut * 1000);
                    WatchDogCounterResetThread = new Thread(new ThreadStart(WatchDogCounterResetLoop));
                    WatchDogCounterResetThread.Start();
#if DebugPrint
                        Debug.Print("Watchdog Thread started!");
#endif
                }
            }
        }
        #endregion

        #region Method SetTime()
        public static void SetTime(int pTimeZoneOffset, string pTimeServer_1, string pTimeServer_2)
        {
            activateWatchdogIfAllowedAndNotYetRunning();
            timeSettings = new Microsoft.SPOT.Time.TimeServiceSettings()
            {
                RefreshTime = 21600,                         // every 6 hours (60 x 60 x 6) default: 300000 sec                    
                AutoDayLightSavings = false,                 // We use our own timeshift calculation
                ForceSyncAtWakeUp = true,
                Tolerance = 30000                            // deviation may be up to 30 sec
            };

            int loopCounter = 1;
            while (loopCounter < 3)
            {
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };
                IPAddress[] address = null;
                IPAddress[] address_2 = null;

                try
                {
                    address = System.Net.Dns.GetHostEntry(pTimeServer_1).AddressList;
                }
                catch { };
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };
                try
                {
                    address_2 = System.Net.Dns.GetHostEntry(pTimeServer_2).AddressList;
                }
                catch { };
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };

                try
                {
                    timeSettings.PrimaryServer = address[0].GetAddressBytes();
                }
                catch { };
                try
                {
                    timeSettings.AlternateServer = address_2[0].GetAddressBytes();
                }
                catch { };

                FixedTimeService.Settings = timeSettings;
                FixedTimeService.SetTimeZoneOffset(pTimeZoneOffset);

                Debug.Print("Starting Timeservice");
                FixedTimeService.Start();
                Debug.Print("Returned from Starting Timeservice");
                Thread.Sleep(100);
                if (DateTime.Now > new DateTime(2016, 7, 1))
                {
                    timeServiceIsRunning = true;
                    Debug.Print("Timeserver intialized on try: " + loopCounter);
                    Debug.Print("Synchronization Interval = " + timeSettings.RefreshTime);
                    break;
                }
                else
                {
                    timeServiceIsRunning = false;
                    Debug.Print("Timeserver could not be intialized on try: " + loopCounter);
                }
                loopCounter++;
            }

            if (timeServiceIsRunning)
            {
                RealTimeClock.SetDateTime(DateTime.Now); //This will set the hardware Real-time Clock
            }
            else
            {
                Debug.Print("No success to get time over internet");
                Utility.SetLocalTime(RealTimeClock.GetDateTime()); // Set System Time to RealTimeClock Time
            }

            //Utility.SetLocalTime(new DateTime(2000, 1, 1, 1, 1, 1));  //For tests, to see what happens when wrong date

            if (DateTime.Now < new DateTime(2016, 7, 1))
            {
                timeIsSet = false;
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(false);  // Reboot the Mainboard
            }
            else
            {
                Debug.Print("Could get Time from Internet or RealTime Clock");
                timeIsSet = true;
            }
        }


        #endregion

        #region TimeService Events
        static void FixedTimeService_SystemTimeChanged(object sender, SystemTimeChangedEventArgs e)
        {
#if DebugPrint
                    Debug.Print("\r\nSystem Time was set");
#endif

#if SD_Card_Logging
                    if (SdLoggerService != null)
                    {
                        var source = new LogContent() { logOrigin = "n", logReason = "System Time was set", logPosition = "n", logNumber = 1 };
                        SdLoggerService.LogEventHourly("Event", source);
                    }
#endif


        }

        static void FixedTimeService_SystemTimeChecked(object sender, SystemTimeChangedEventArgs e)
        {
#if DebugPrint
                Debug.Print("\r\nSystem Time was checked");
#endif
#if SD_Card_Logging
                if (SdLoggerService != null)
                {
                    var source = new LogContent() { logOrigin = "n", logReason = "System Time was checked", logPosition = "n", logNumber = 1 };
                    SdLoggerService.LogEventHourly("Event", source);
                }
#endif

        }
        #endregion

        #region WatchDogCounterResetLoop
        static void WatchDogCounterResetLoop()
        {
            int testCounter = 0;
            while (true)
            {
                /*
                if (testCounter > 10)
                {
                    Thread.Sleep((watchDogTimeOut * 1000) + 1500);  // let the watchdog run out
                    testCounter = 0;
                }
                else
                {
                */
                // reset time counter 800 msec before it runs out
                Thread.Sleep((watchDogTimeOut * 1000) - 2000);
                //try { GHI.Processor.Watchdog.ResetCounter(); }
                //catch { };
#if DebugPrint
                    Debug.Print("\r\nWachdogtimer reset! " + testCounter);
#endif
                testCounter++;
                //}
            }
        }
        #endregion

        #region _Print_Debug
        private static void _Print_Debug(string message)
        {
            //lock (theLock1)
            //{
#if DebugPrint
                        Debug.Print(message);
#endif

            /*
            switch (_AzureDebugMode)
            {
                //Do nothing             
                case AzureStorageHelper.DebugMode.NoDebug:
                    break;

                //Output Debugging info to the serial port
                case AzureStorageHelper.DebugMode.SerialDebug:

                    //Convert the message to bytes
                    
                    //byte[] message_buffer = System.Text.Encoding.UTF8.GetBytes(message);
                    //_debug_port.Write(message_buffer,0,message_buffer.Length);
                    
                    break;

                //Print message to the standard debug output
                case AzureStorageHelper.DebugMode.StandardDebug:
#if DebugPrint
                        Debug.Print(message);
#endif
                    break;
            }
            */
        }
        
        #endregion
    

    }
    }


