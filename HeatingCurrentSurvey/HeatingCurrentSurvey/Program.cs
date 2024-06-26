// HeatingCurrentSurvey Program Copyright RoSchmi 2021, 2022 License Apache 2.0,  Version 1.6 vom 27. Juli 2024, 
// NETMF 4.3, GHI SDK 2016 R1
// Hardware: GHI Spider Mainboard, Ethernet J11D Ethernet module, Sharp PC900V Optokoppler 
// Dieses Programm dient zur Registrierung der Laufzeiten eines Heizungsbrenners,
// der Boilerheizungspumpe zur Brauchwassererwärmung, der Pumpe einer Solarthermieanlage und dem
// gemessenen Strom eines Smartmeters, der Messung von 3 Temperaturen einer Solarthermieanlage
// und der erzeugten Energie eines Solarmoduls. 
// Wenn der Brenner läuft wird der Eingang durch die Interface Elektronik auf low gezogen
// Bei jedem Wechsel des Status wird ein Datensatz mit Timestamp und den bisher gelaufenen Zeiten
// (Tag, Woche, Monat und Jahr) in der Azure Cloud in einer Storage Table abgelegt.
// Zusätzlich wird in einer anderen Tabelle einmal am Ende jeden Tages ein Datensatz mit der Laufzeit dieses Tages
// abgelegt.
// Zur Verwendung des Gadgeteer Spider Mainboard im Netzwerk muss zuerst die MAC Adresse gesetzt werden,
// Anleitung siehe im Internet: Setting up Ethernet on the .NET Gadgeteer by Pete Brown
// The first step is to set up the board in MFDeploy. You can find MFDeploy in the .NET Micro Framework SDK Tools 
// folder off your start menu. Connect to your board and then use the Target -> Configuration -> Network menu option. 
// The only thing you need to set in this dialog is the MAC address; make sure it's the same address as shown on the sticker 
// on your board. This may not be strictly necessary in all cases, but you want them to match, and you will need a valid 
// MAC address for most routers.
// Zur sicheren Datenübertragung mit https muss mittels des Programms 'MfDeploy' die SSL-Seed vorbesetzt werden
//
// Gegenüber der Vorgängerversion wurde die Auswertung der Daten eines Smartmeters zur Stromverbrauchsmessung integriert
// Gegenüber der Vorgängerversion wurde Auswertung der Temperaturen der Solarthermieanlage integriert
// Gegenüber der Vorgängerversion wurde die Auswertung der Energiegewinnung einer Solarzelle über die Fritzbox und eine
// Fritz!Dect schaltbare Steckdose integriert.
//
// Die Daten von Solarpumpe, Smartmeter und Solarthermie werden von separaten Micrcontroller-Nodes über Rfm69 Funk übertragen
// Eine Anwendung läuft auf einem FeatherM0 Rfm69 Board (Feather_Rfm69_Power_a_Heating_Survey)
// Die zweite Anwendung läuft auf einem Moteino Rfm69 Board (MoteinoReadAnalogRfm69)
// Verlauf: 1) Es wurde eine zweite Tabelle zur Überwachung der Speicherpumpe realisiert
// Verlauf: 2) Umfangreiche Änderungen mit base Klassen für AzureSendManager und SensorMgr
// Verlauf: 3) Zusätzlicher Rfm69 Empfänger um Sensordaten, die per Funk übermittelt werden darzustellen
// Verlauf: 4) Der Rowkey entspricht nun der lokalen Zeitzonen-Zeit unter Berücksichtigung der Sommerzeit
// Verlauf: 5) Der Spalte SampleTime wurde als Anhang die aktuelle Verschiebung gegenüber UTC als Anhang beigefügt
// Verlauf: 6) Zusätzliche Tabelle (Solar) zur Überwachung der Pumpe der Solarthermieanlage
// Verlauf: 7) Zusätzliche Tabelle zur Überwachung des Stromverbrauchs durch Auslesung der Daten eines Smartmeters
// Verlauf: 8) Zusätzliche Tabelle zu Überwachung von drei Temperaturen der Solarthermieanlage (Collector, Speicher, Brauchwasser)
// Verlauf: 9) Zusätzliche Abfrage der Fritz!Dect Steckdose über die Fritzbox und Speicherung in der Cloud zusammen mit den Stromverbrauchsdaten
// Verlauf: 10) Implementierung eines "_sensorControlTimer", um das board zu rebooten falls keine events mit Stromverbrauchsdaten des Rfm69 Empfängers erfolgen (hangs)
// Verlauf: 11) Umstellung auf DigiCertGlobalRootG2 Zertifikat für https Zugriff auf Azure Table Storage

//#define DebugPrint

using System;
using System.Threading;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
using HeatingSurvey;
using RoSchmi.Net.Azure.Storage;
using RoSchmi.ButtonNETMF;
using RoSchmi.DayLihtSavingTime;
using RoSchmi.RFM69_NETMF;
using RoSchmi.Utilities;
using RoSchmi.Net.Fritzbox;
using PervasiveDigital;
using PervasiveDigital.Json;
//using HeatingCurrentSurvey;


namespace HeatingCurrentSurvey
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
        // private static bool workWithWatchDog = true;    // Choose whether the App runs with WatchDog, should normally be set to true
        private static bool workWithWatchDog = false; 
        private static int watchDogTimeOut = 50;        // WatchDog timeout in sec: Max Value for G400 15 sec, G120 134 sec, EMX 4.294 sec
        // = 50 sec, don't change without need, may not be below 30 sec 

        static Timer _sensorControlTimer;
        static TimeSpan _sensorControlTimerInterval = new TimeSpan(0, 35, 0);  // 35 minutes
        // The event handler of this  timer checks if there was an event of the solarPumpCurrentSensor
        // in the selected time (here 35 min). This means that the program is running but receiving data via RF69 hangs 
        // in this very rarely occuring case we reset the board



        // If the free ram of the mainboard is below this level it will reboot (because of https memory leak)
        private static int freeRamThreshold = 4300000;
        //private static int freeRamThreshold = 3300000;

        // You can select what kind of Debug.Print messages are sent

        //public static AzureStorageHelper.DebugMode _AzureDebugMode = AzureStorageHelper.DebugMode.StandardDebug;
        public static AzureStorageHelper.DebugMode _AzureDebugMode = AzureStorageHelper.DebugMode.NoDebug;
        public static AzureStorageHelper.DebugLevel _AzureDebugLevel = AzureStorageHelper.DebugLevel.DebugAll;

        // To use Fiddler as WebProxy set attachFiddler = true and set the proper IPAddress and port
        // Use the local IP-Address of the PC where Fiddler is running
        // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
        private static bool attachFiddler = false;
        private const string fiddlerIPAddress = "192.168.1.21"; // Set to the IP-Adress of your PC
        private const int fiddlerPort = 8888;                   // Standard port of fiddler

        // End of Common Settings
        #endregion

        #region Setting concerning your Azure Table Storage Account

        // Set your Azure Storage Account Credentials here or store them in the Resources      
        static string myAzureAccount = Resources.GetString(Resources.StringResources.AzureAccountName);
        //static string myAzureAccount = "your Accountname";
        

        static string myAzureKey = Resources.GetString(Resources.StringResources.AzureAccountKey);
        //static string myAzureKey = "your key";
       

        // choose whether http or https shall be used
           private const bool Azure_useHTTPS = true;
           // const bool Azure_useHTTPS = false;
       

        // Preset for the Name of the Azure storage table 
        // To build the table name, the table prefix is augmented with the actual year
        // So data from one year can be easily deleted
        // A second table is generated, the name of this table is augmented with the suffix "Days" and the actual year (e.g. TestDays2018)
        //

        
        
        private static string _tablePreFix_Burner = "Brenner";          // Preset, comes in the sensor eventhandler
        private static string _tablePreFix_Boiler = "BoilerHeizung";    // Preset, comes in the sensor eventhandle
        private static string _tablePreFix_Solar = "Solar";             // Preset, comes in the sensor eventhandle
        private const string  _tablePreFix_Current = "Current";         // Preset, comes in the sensor eventhandle
        private const string _tablePreFix_SolarTemps = "SolarTemps";    // Preset, comes in the sensor eventhandle

        private const string  _tablePreFix_Definition_3 = "SolarTemps";   // Preset, doesn't come in the sensor eventhandle

        

        // Preset for the partitionKey of the table entities.        
        private static string _partitionKeyPrefix_Burner = "Y3_";   // Preset, comes in the sensor eventhandler
        private static string _partitionKeyPrefix_Boiler = "Y3_";
        private static string _partitionKeyPrefix_Solar = "Y3_";
        private static string _partitionKey_Current = "Y2_";


        // if augmentPartitionKey == true, the actual year and month are added, e.g. Y_2016_07
        private static bool augmentPartitionKey = true;
        

        private static string _location_Burner = "Heizung";              // Preset, can be replaced with a value received in the sensor eventhandler
        private static string _location_Boiler = "Heizung";
        private static string _location_Solar  = "Heizung";
        private static string _location_Current = "Keller"; 

        private static string _sensorValueHeader_Burner = "OnOff";
        private static string _sensorValueHeader_Boiler = "OnOff";
        private static string _sensorValueHeader_Solar  = "OnOff";     
        private static string _sensorValueHeader_Current = "T_0";
        private static string _sensorValueHeader_SolarTemps = "Coll";



        private static string _socketSensorHeader_Burner = "";  // (not used in this App)
        private static string _socketSensorHeader_Boiler = "";  // (not used in this App)
        private static string _socketSensorHeader_Solar = "";  // (not used in this App)
        private static string _socketSensorHeader_Current = "Current";
        private static string _socketSensorHeader_SolarTemps = "ST";


        #endregion

        
        #region Settings concerning Rfm69 receiver

        // Settings for Rfm69 (like Node IDs of sender and recipient) must be set in Class OnOffRfm69SensorMgr.cs

        static int Ch_1_Sel = 1;   // The Channel of the temp/humidity sensor (Values from 1 to 8 are allowed)
        static int Ch_2_Sel = 2;
        static int Ch_3_Sel = 3;
        static int Ch_4_Sel = 4;
        static int Ch_5_Sel = 5;
        static int Ch_6_Sel = 6;
        static int Ch_7_Sel = 7;
        static int Ch_8_Sel = 8;

        
        #endregion
       
        #endregion

        #region Fields

        static AutoResetEvent waitForCurrentCallback = new AutoResetEvent(false);
        static AutoResetEvent waitForSolarTempsCallback = new AutoResetEvent(false);

        public static SDCard SD;
        private static bool _fs_ready = false;

        public static ButtonNETMF myButton;
        public static ButtonNETMF LDR1Button;

        private static OutputPort _Led;

         //private static GHI.Networking.EthernetENC28J60 netif;
        private static GHI.Networking.EthernetBuiltIn netif;

        private static bool _hasAddress;
        private static bool _available;

        private static bool watchDogIsAcitvated = false;// Don't change, choosing is done in the workWithWatchDog variable
        
        static Thread WatchDogCounterResetThread;
        
        private static Counters _counters = new Counters();       
                       
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
        static Regex _tableRegex = new Regex(@"^[a-zA-Z0-9]+$");
        static Regex _columnRegex = new Regex(@"^[a-zA-Z0-9_]+$");
        static Regex _stringRegex = new Regex(@"^[^<>]+$");

        private static int _azureSendThreads = 0;
        private static int _azureSendThreadResetCounter = 0;
               

        // Certificate of Azure, included as a Resource
        //static byte[] caAzure = Resources.GetBytes(Resources.BinaryResources.DigiCert_Baltimore_Root);
        static byte[] caAzure = Resources.GetBytes(Resources.BinaryResources.DigiCertGlobalRootG2);

        // See -https://blog.devmobile.co.nz/2013/03/01/https-with-netmf-http-client-managing-certificates/ how to include a certificate

        private static X509Certificate[] caCerts;

        private static IPAddress localIpAddress = null;
        private Microsoft.SPOT.Net.NetworkInformation.NetworkInterface settings;

        private static TimeServiceSettings timeSettings;
        private static bool timeServiceIsRunning = false;
        private static bool timeIsSet = false;

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

        static string fritzBoxUser = Resources.GetString(Resources.StringResources.FritzBoxUser);
        static string fritzBoxPassword = Resources.GetString(Resources.StringResources.FritzBoxPassword);
        static string fritzUrl = Resources.GetString(Resources.StringResources.FritzBoxUrl);
        static bool useHttps = false;


        static string FRITZ_DEVICE_AIN_01 = Resources.GetString(Resources.StringResources.FritzDect_Ain_01); // e.g. "126580760143" Ain of Fritz!Dect switchable power socket

        static NETMF_FritzAPI fritz = new NETMF_FritzAPI(fritzBoxUser, fritzBoxPassword, fritzUrl, useHttps);


        #endregion

        #region Main
        public static void Main()
        {

            

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

            caCerts = new X509Certificate[] { new X509Certificate(caAzure) };

            int timeOutMs = 100000;    // Wait for Timeserver Response
            long startTicks = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            long timeOutTicks = timeOutMs * TimeSpan.TicksPerMillisecond + startTicks;
            while ((!timeIsSet) && (Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks < timeOutTicks))
            {
                Thread.Sleep(100);
            }
            if (!timeIsSet)           // for the case that there was no AddressChanged Event, try to set time
            {
                SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
            }
            long endTicks = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            if (!timeIsSet)
            {
                Debug.Print("Going to reboot in 20 sec. Have waited for " + (endTicks - startTicks) / TimeSpan.TicksPerMillisecond + " ms.\r\n");
                Thread.Sleep(20000);
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
                while (true)
                {
                    Thread.Sleep(100);
                }
            }
            else
            {
                Debug.Print("Program continues. Waited for time for " + (endTicks - startTicks) / TimeSpan.TicksPerMillisecond + " ms.\r\n");
                Debug.Print("Got Time from " + (timeServiceIsRunning ? "Internet" : "Hardware RealTimeClock"));
            }

            #endregion

            // Initialize Access to Fritzbox (get SID and store in NETMF_FritzApi)
            if (fritz.init())
            {
                Debug.Print("Fritz is initialized");
            }

           
            #region Set some Presets for Azure Table and others
           
            myCloudStorageAccount = new CloudStorageAccount(myAzureAccount, myAzureKey, useHttps: Azure_useHTTPS);

            // Initialization for each table must be done in main
            myAzureSendManager_Burner = new AzureSendManager_Burner(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, _tablePreFix_Burner, _sensorValueHeader_Burner, _socketSensorHeader_Burner, caCerts, DateTime.Now, sendInterval_Burner, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            AzureSendManager_Burner._lastResetCause = _lastResetCause;                  
            AzureSendManager_Burner.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            AzureSendManager_Burner.InitializeQueue();

            myAzureSendManager_Boiler = new AzureSendManager_Boiler(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, _tablePreFix_Boiler, _sensorValueHeader_Boiler, _socketSensorHeader_Boiler, caCerts, DateTime.Now, sendInterval_Boiler, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            AzureSendManager_Boiler._lastResetCause = _lastResetCause;
            AzureSendManager_Boiler.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            AzureSendManager_Boiler.InitializeQueue();

            myAzureSendManager_Solar = new AzureSendManager_Solar(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, _tablePreFix_Solar, _sensorValueHeader_Solar, _socketSensorHeader_Solar, caCerts, DateTime.Now, sendInterval_Solar, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            AzureSendManager_Solar._lastResetCause = _lastResetCause;
            AzureSendManager_Solar.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            AzureSendManager_Solar.InitializeQueue();

            myAzureSendManager = new AzureSendManager(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, _tablePreFix_Current, _sensorValueHeader_Current, _socketSensorHeader_Current, caCerts, DateTime.Now, sendInterval_Current, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            AzureSendManager.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            AzureSendManager.InitializeQueue();

            myAzureSendManager_SolarTemps = new AzureSendManager_SolarTemps(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, _tablePreFix_SolarTemps, _sensorValueHeader_SolarTemps, _socketSensorHeader_SolarTemps, caCerts, DateTime.Now, sendInterval_SolarTemps, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            AzureSendManager_SolarTemps.sampleTimeOfLastSent = DateTime.Now.AddDays(-10.0);    // Date in the past
            AzureSendManager_SolarTemps.InitializeQueue();
            


            
            _willRebootAfterNextEvent = false;
            
            _azureSendThreads = 0;
            _azureSendThreadResetCounter = 0;
            
            
            #endregion

            //_sensorControlTimer = new Timer(new TimerCallback(_sensorControlTimer_Tick), cls, _sensorControlTimerInterval, _sensorControlTimerInterval);
            _sensorControlTimer = new Timer(new TimerCallback(_sensorControlTimer_Tick), null, _sensorControlTimerInterval, _sensorControlTimerInterval);

            if (_deviceType == GHI.Processor.DeviceType.EMX)
            {
                myButton = new ButtonNETMF(GHI.Pins.FEZSpider.Socket12.Pin3, GHI.Pins.FEZSpider.Socket12.Pin4);
            }
            else
            {
                if (_deviceType == GHI.Processor.DeviceType.G120E)
                {
                    myButton = new ButtonNETMF(GHI.Pins.FEZSpiderII.Socket12.Pin3, GHI.Pins.FEZSpiderII.Socket12.Pin4);
                }
                else
                {
                    throw new NotSupportedException("Mainbboard not supported");
                }
            }

           // myButton.ButtonPressed += myButton_ButtonPressed;
           // myButton.ButtonReleased += myButton_ButtonReleased;
                               
            myBurnerSensor = new OnOffDigitalSensorMgr(DeviceType.EMX, 4, dstOffset, dstStart, dstEnd, _partitionKeyPrefix_Burner, _location_Burner,  _sensorValueHeader_Burner, _tablePreFix_Burner, "0");
            myBurnerSensor.digitalOnOffSensorSend += myBurnerSensor_digitalOnOffSensorSend;
            
            myStoragePumpSensor = new OnOffAnalogSensorMgr(DeviceType.EMX, 9, 10, dstOffset, dstStart, dstEnd, _partitionKeyPrefix_Boiler, _location_Boiler, _sensorValueHeader_Boiler, _tablePreFix_Boiler, "0");    
            myStoragePumpSensor.currentSensorSend += myStoragePumpSensor_currentSensorSend;

            mySolarPumpCurrentSensor = new OnOffRfm69SensorMgr(DeviceType.EMX, 6, dstOffset, dstStart, dstEnd, _partitionKeyPrefix_Solar, _location_Solar, _sensorValueHeader_Solar, _sensorValueHeader_Current, _tablePreFix_Solar, _tablePreFix_Current, "0");

            mySolarPumpCurrentSensor.rfm69OnOffSensorSend += mySolarPumpCurrentSensor_rfm69OnOffSensorSend;
            mySolarPumpCurrentSensor.rfm69DataSensorSend += mySolarPumpCurrentSensor_rfm69DataSensorSend;
            mySolarPumpCurrentSensor.rfm69SolarTempsDataSensorSend += mySolarPumpCurrentSensor_rfm69SolarTempsDataSensorSend;
           

            activateWatchdogIfAllowedAndNotYetRunning();

            myBurnerSensor.Start();
            myStoragePumpSensor.Start();
            mySolarPumpCurrentSensor.Start();

            // finally: blinking a LED, just for fun
            _Led = new OutputPort(_deviceType == GHI.Processor.DeviceType.EMX ? GHI.Pins.FEZSpider.DebugLed : GHI.Pins.FEZSpiderII.DebugLed, true);
            while (true)
            {
                _Led.Write(true);
                Thread.Sleep(200);
                _Led.Write(false);
                Thread.Sleep(200);
            }          
        }

        #endregion

        #region Event SolarPumpSolarTempsDataSensor_SignalReceived
        static void _sensorControlTimer_Tick(object o)
        {
            Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
        }

        #endregion


        #region Event SolarPumpSolarTempsDataSensor_SignalReceived
        static void mySolarPumpCurrentSensor_rfm69SolarTempsDataSensorSend(OnOffRfm69SensorMgr sender, OnOffRfm69SensorMgr.DataSensorEventArgs e)
        {
            // This eventhandler is for the case when Continuous Sensordata from the solar thermal system were sent
            string outString = string.Empty;
            bool forceSend = false;

            // The magic word EscapeTableLocation_03 makes, that not the tabelname from e is used, but from the const _tablePreFix_Definition_3
            string tablePreFix = (e.DestinationTable == "EscapeTableLocation_03") ? _tablePreFix_Definition_3 : e.DestinationTable;

            double dayMaxBefore = AzureSendManager_SolarTemps._dayMax < 0 ? 0.00 : AzureSendManager_SolarTemps._dayMax;
            double dayMinBefore = AzureSendManager_SolarTemps._dayMin > 70 ? 0.00 : AzureSendManager_SolarTemps._dayMin;
     

            double decimalValue = (double)(((double)e.Val_1 / 10) - 70);      // T1
            decimalValue = ((decimalValue > 170) || (decimalValue < -40)) ? InValidValue : (decimalValue > 140) ? 140.0 : decimalValue;

            double t2_decimal_value = (double)((double)(e.Val_2 - 700) / 10);   // T_2
            t2_decimal_value = ((t2_decimal_value > 140) || (t2_decimal_value < -40)) ? InValidValue : t2_decimal_value;

            double logCurrent = decimalValue;
            double t3_decimal_value = (double)((double)(e.Val_3 - 700) / 10);   // T_3
            t3_decimal_value = ((t3_decimal_value > 140) || (t3_decimal_value < -40)) ? InValidValue : t3_decimal_value;

            int MacId = 0;    // To monitor if the MAC-Address always stays the same, we write the sum of their bytes in T4
            foreach (byte place in netif.PhysicalAddress)
            {
                MacId += place;
            }

            double t4_decimal_value = (double)MacId;         // T4
            double t5_decimal_value = InValidValue;          // T_5
            double t6_decimal_value = InValidValue;          // T_6

           

            

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: RF 433 Signal received", logReason = "n", logPosition = "RF 433 event Start", logNumber = 1 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif

#if DebugPrint
            Debug.Print("Rfm69 event, Collector: " + decimalValue.ToString("f2") + "°C, Storage: " + t2_decimal_value.ToString("f2") + "°C, Water: " + t3_decimal_value.ToString("f2") + "°C");
#endif

            activateWatchdogIfAllowedAndNotYetRunning();

            #region Preset some table parameters like Row Headers
            // Here we set some table parameters which where transmitted in the eventhandler and were set in the constructor of the RF_433_Receiver Class

            //_tablePreFix_Current = e.DestinationTable;

            _partitionKey_Current = e.SensorLabel;
            _location_Current = e.SensorLocation;
            _sensorValueHeader_Current = e.MeasuredQuantity;
            _socketSensorHeader_Current = "NU";
            #endregion


            DateTime timeOfThisEvent = DateTime.Now;
            AzureSendManager_SolarTemps._timeOfLastSensorEvent = timeOfThisEvent;    
            // when no sensor events occure in a certain timespan

            string switchMessage = "Switch Message Preset";
            string switchState = "???";
            string actCurrent = "???";

            #region Test if timeService is running. If not, try to initialize
            if (!timeServiceIsRunning)
            {
                if (DateTime.Now < new DateTime(2016, 7, 1))
                {
#if DebugPrint
                        Debug.Print("Going to set the time in rf_433_Receiver_SignalReceived event");
#endif
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
                    Thread.Sleep(200);
                }
                else
                {
                    timeServiceIsRunning = true;
                }
                if (!timeServiceIsRunning)
                {
#if DebugPrint
                        Debug.Print("Sending aborted since timeservice is not running");
#endif
                    return;
                }
            }
            #endregion

            #region Do some tests with RegEx to assure that proper content is transmitted to the Azure table

            RegexTest.ThrowIfNotValid(_tableRegex, new string[] { tablePreFix, _location_Current});
            RegexTest.ThrowIfNotValid(_columnRegex, new string[] {_sensorValueHeader_Current });

            #endregion


           

            #region After a reboot: Read the last stored entity from Azure to actualize the counters
            if (AzureSendManager_SolarTemps._iteration == 0)    // The system has rebooted: We read the last entity from the Cloud
            {
                _counters = myAzureSendManager_SolarTemps.ActualizeFromLastAzureRow(ref switchMessage);
                

                _azureSendErrors = _counters.AzureSendErrors > _azureSendErrors ? _counters.AzureSendErrors : _azureSendErrors;
                _azureSends = _counters.AzureSends > _azureSends ? _counters.AzureSends : _azureSends;
                _forcedReboots = _counters.ForcedReboots > _forcedReboots ? _counters.ForcedReboots : _forcedReboots;
                _badReboots = _counters.BadReboots > _badReboots ? _counters.BadReboots : _badReboots;
                //_dayMinWorkBefore = _dayMinWork;
                //_dayMinWork = _counters.DayMinWork > _dayMinWork ? _counters.DayMinWork : _dayMinWork;

                forceSend = true;
                // actualize to consider the timedelay caused by reading from the cloud
                timeOfThisEvent = DateTime.Now;
                AzureSendManager_SolarTemps._timeOfLastSensorEvent = timeOfThisEvent;
            }
            #endregion

            // when every sample value shall be sent to Azure, remove the outcomment of the next two lines
            //forceSend = true;                 
            //switchMessage = "Sending was forced by Program";              

            #region Check if we still have enough free ram (memory leak in https) and evtl. prepare for resetting the mainboard
            uint remainingRam = Debug.GC(false);            // Get remaining Ram because of the memory leak in https
            bool willReboot = (remainingRam < freeRamThreshold);     // If the ram is below this value, the Mainboard will reboot
            if (willReboot)
            {
                forceSend = true;
                switchMessage = "Going to reboot the Mainboard due to not enough free RAM";
            }
            #endregion

            DateTime copyTimeOfLastSend = AzureSendManager_SolarTemps._timeOfLastSend;

            //copyTimeOfLastSend = copyTimeOfLastSend.AddDays(-1);

            TimeSpan timeFromLastSend = timeOfThisEvent - copyTimeOfLastSend;

            double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);

            #region Set the partitionKey
            //string partitionKey = _partitionKey_Current;                    // Set Partition Key for Azure storage table
            string partitionKey = e.SensorLabel;                    // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                        // if wanted, augment with year and month (12 - month for right order)
            //{ partitionKey = partitionKey + DateTime.Now.ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.Month).ToString(), 2); }
            { partitionKey = partitionKey + DateTime.Now.AddMinutes(daylightCorrectOffset).ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.AddMinutes(daylightCorrectOffset).Month).ToString(), 2); }
            #endregion

            #region Regex test for proper content of the Message property in the Azure table
            // The regex test can be outcommented if the string is valid
            if (!_stringRegex.IsMatch(switchMessage))
            { throw new NotSupportedException("Some charcters [<>] may not be used in this string"); }
            #endregion


            #region If sendInterval has expired, write new sample value row to the buffer and start writing to Azure
            if ((timeFromLastSend > AzureSendManager_SolarTemps._sendInterval) || forceSend)
            {
                #region actualize the values of minumum and maximum measurements of the day


                //RoSchmi
               
                if (AzureSendManager_SolarTemps._timeOfLastSend.AddMinutes(daylightCorrectOffset).Day == timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day)               
                {
                    if ((decimalValue > AzureSendManager_SolarTemps._dayMax) && (decimalValue < 140.0))
                    {
                        AzureSendManager_SolarTemps._dayMax = decimalValue;
                    }
                    if ((decimalValue > -39.0) && (decimalValue < AzureSendManager_SolarTemps._dayMin))
                    {
                        AzureSendManager_SolarTemps._dayMin = decimalValue;
                    }
                }
                else
                {
                    // first event of a new day
                    if ((decimalValue > -39.0) && (decimalValue < 140.0))
                    {
                        AzureSendManager_SolarTemps._dayMax = decimalValue;
                        AzureSendManager_SolarTemps._dayMin = decimalValue;
                    }
                }
                #endregion

                AzureSendManager_SolarTemps._lastValue = decimalValue;

                for (int i = 0; i < 8; i++)
                {
                    //_sensorValueArr_Out[i] = new SensorValue(_timeOfLastSensorEvent, 0, 0, 0, 0, InValidValue, 999, 0x00, false);
                    _sensorValueArr_Out[i] = new SensorValue(AzureSendManager_SolarTemps._timeOfLastSensorEvent, 0, 0, 0, 0, InValidValue, 999, 0x00, false);

                }
                _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = decimalValue;            // T_1 
                _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble = t2_decimal_value;        // T_2 
                _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble = t3_decimal_value;        // T_3
                _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble = t4_decimal_value;        // T_4 
                _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble = t5_decimal_value;        // T_5 
                _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble = t6_decimal_value;        // T_6


                AzureSendManager_SolarTemps._iteration++;

                SampleValue theRow = new SampleValue(tablePreFix + DateTime.Now.Year, partitionKey, e.Timestamp, timeZoneOffset + (int)daylightCorrectOffset, logCurrent, AzureSendManager_SolarTemps._dayMin, AzureSendManager_SolarTemps._dayMax,
                   _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_1_Sel - 1].RandomId, _sensorValueArr_Out[Ch_1_Sel - 1].Hum, _sensorValueArr_Out[Ch_1_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_2_Sel - 1].RandomId, _sensorValueArr_Out[Ch_2_Sel - 1].Hum, _sensorValueArr_Out[Ch_2_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_3_Sel - 1].RandomId, _sensorValueArr_Out[Ch_3_Sel - 1].Hum, _sensorValueArr_Out[Ch_3_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_4_Sel - 1].RandomId, _sensorValueArr_Out[Ch_4_Sel - 1].Hum, _sensorValueArr_Out[Ch_4_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_5_Sel - 1].RandomId, _sensorValueArr_Out[Ch_5_Sel - 1].Hum, _sensorValueArr_Out[Ch_5_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_6_Sel - 1].RandomId, _sensorValueArr_Out[Ch_6_Sel - 1].Hum, _sensorValueArr_Out[Ch_6_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_7_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_7_Sel - 1].RandomId, _sensorValueArr_Out[Ch_7_Sel - 1].Hum, _sensorValueArr_Out[Ch_7_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_8_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_8_Sel - 1].RandomId, _sensorValueArr_Out[Ch_8_Sel - 1].Hum, _sensorValueArr_Out[Ch_8_Sel - 1].BatteryIsLow,
                   actCurrent, switchState, _location_Current, timeFromLastSend, e.RepeatSend, e.RSSI, AzureSendManager_SolarTemps._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, forceSend ? switchMessage : "");


                if (AzureSendManager_SolarTemps._iteration == 1)
                {
                    if (timeFromLastSend < makeInvalidTimeSpan)   // after reboot for the first time take values which were read back from the Cloud
                    {
                        //theRow.T_0 = AzureSendManager_SolarTemps._lastContent[Ch_1_Sel - 1];
                        //theRow.T_1 = AzureSendManager_SolarTemps._lastContent[Ch_2_Sel - 1];
                          theRow.T_2 = AzureSendManager_SolarTemps._lastContent[Ch_3_Sel - 1];
                        //theRow.T_3 = AzureSendManager_SolarTemps._lastContent[Ch_4_Sel - 1];
                        //theRow.T_4 = AzureSendManager_SolarTemps._lastContent[Ch_5_Sel - 1];
                          theRow.T_5 = AzureSendManager_SolarTemps._lastContent[Ch_6_Sel - 1];
                        //theRow.T_6 = AzureSendManager_SolarTemps._lastContent[Ch_7_Sel - 1];
                        //theRow.T_7 = AzureSendManager_SolarTemps._lastContent[Ch_8_Sel - 1];
                    }
                    else
                    {
                        //theRow.T_0 = InValidValue;
                        //theRow.T_1 = InValidValue;
                        theRow.T_2 = InValidValue;
                        //theRow.T_3 = InValidValue;
                        //theRow.T_4 = InValidValue;
                        theRow.T_5 = InValidValue;
                        //theRow.T_6 = InValidValue;
                        //theRow.T_7 = InValidValue;
                    }
                }


                if (AzureSendManager_SolarTemps.hasFreePlaces())
                {
                    AzureSendManager_SolarTemps.EnqueueSampleValue(theRow);

                    // RoSchmi
                    //copyTimeOfLastSend = timeOfThisEvent;
                    AzureSendManager_SolarTemps._timeOfLastSend = timeOfThisEvent;

                    //Debug.Print("\r\nRow was writen to the Buffer. Number of rows in the buffer = " + AzureSendManager.Count + ", still " + (AzureSendManager.capacity - AzureSendManager.Count).ToString() + " places free");
                }
                // optionally send message to Debug.Print  
                //SampleValue theReturn = AzureSendManager.PreViewNextSampleValue();
                //DateTime thatTime = theReturn.TimeOfSample;
                //double thatDouble = theReturn.TheSampleValue;
                //Debug.Print("The Temperature: " + thatDouble.ToString() + "  at: " + thatTime.ToString());

            #endregion


                #region ligth a multicolour led asynchronously to indicate action
#if MulticolorLed
                myMulticolorLedAsync.light("green", 3000);
#endif
                #endregion

                #region If sendInterval has expired, send contents of the buffer to Azure
                if (_azureSendThreads < 5)
                //if (_azureSendThreads == 0)
                {
                    lock (MainThreadLock)
                    {
                        _azureSendThreads++;
                    }
                    // RoSchmi
                    myAzureSendManager_SolarTemps = new AzureSendManager_SolarTemps(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, tablePreFix, _sensorValueHeader_SolarTemps, _socketSensorHeader_SolarTemps, caCerts, timeOfThisEvent, sendInterval_Current, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                    myAzureSendManager_SolarTemps.AzureCommandSend += myAzureSendManager_SolarTemps_AzureCommandSend;
                        
                    
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    _Print_Debug("\r\nRow was sent on its way to Azure");
                    myAzureSendManager_SolarTemps.Start();

                    //RoSchmi
                    // if last send was yesterday: write                  
                    var last = copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).AddDays(1).Day;

                    var theAct = timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day;

                    if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).AddDays(1).Day == timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            _sensorValueArr_Out[i] = new SensorValue(copyTimeOfLastSend, 0, 0, 0, 0, InValidValue, 999, 0x00, false);

                        }

                        
                        _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = AzureSendManager_SolarTemps._dayMin;
                        
                        _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble = AzureSendManager_SolarTemps._dayMax;


                        forceSend = true;


                        theRow = new SampleValue(tablePreFix, partitionKey, copyTimeOfLastSend.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset,
                            copyTimeOfLastSend, true)), timeZoneOffset + (int)daylightCorrectOffset, 0.00, dayMinBefore, dayMaxBefore,
                           _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_1_Sel - 1].RandomId, _sensorValueArr_Out[Ch_1_Sel - 1].Hum, _sensorValueArr_Out[Ch_1_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_2_Sel - 1].RandomId, _sensorValueArr_Out[Ch_2_Sel - 1].Hum, _sensorValueArr_Out[Ch_2_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_3_Sel - 1].RandomId, _sensorValueArr_Out[Ch_3_Sel - 1].Hum, _sensorValueArr_Out[Ch_3_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_4_Sel - 1].RandomId, _sensorValueArr_Out[Ch_4_Sel - 1].Hum, _sensorValueArr_Out[Ch_4_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_5_Sel - 1].RandomId, _sensorValueArr_Out[Ch_5_Sel - 1].Hum, _sensorValueArr_Out[Ch_5_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_6_Sel - 1].RandomId, _sensorValueArr_Out[Ch_6_Sel - 1].Hum, _sensorValueArr_Out[Ch_6_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_7_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_7_Sel - 1].RandomId, _sensorValueArr_Out[Ch_7_Sel - 1].Hum, _sensorValueArr_Out[Ch_7_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_8_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_8_Sel - 1].RandomId, _sensorValueArr_Out[Ch_8_Sel - 1].Hum, _sensorValueArr_Out[Ch_8_Sel - 1].BatteryIsLow,
                           " ", " ", _location_Current, new TimeSpan(0), e.RepeatSend, e.RSSI, AzureSendManager_SolarTemps._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, forceSend ? switchMessage : "");


                        waitForCurrentCallback.Reset();
                        waitForCurrentCallback.WaitOne(50000, true);

                        Thread.Sleep(5000); // Wait additional 5 sec for last thread AzureSendManager Thread to finish
                        AzureSendManager_SolarTemps.EnqueueSampleValue(theRow);
                        // RoSchmi
                        myAzureSendManager_SolarTemps = new AzureSendManager_SolarTemps(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable + "Days", "Work", _socketSensorHeader_Current, caCerts, timeOfThisEvent, sendInterval_Current, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                        myAzureSendManager_SolarTemps.AzureCommandSend += myAzureSendManager_SolarTemps_AzureCommandSend; 
                           
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        //_Print_Debug("\r\nRow was sent on its way to Azure");
#if DebugPrint
                            Debug.Print("\r\nLast Row of day was sent on its way to Azure");
#endif

                        myAzureSendManager_SolarTemps.Start();

                    }
                    
                }
                else
                {
                    _azureSendThreadResetCounter++;
#if DebugPrint
                        Debug.Print("_azureSendThreadResetCounter = " + _azureSendThreadResetCounter.ToString());
#endif
                    if (_azureSendThreadResetCounter > 5)   // when _azureSendThread != 0 we write the next 5 rows coming through sensor events only to the buffer
                    // this should give outstanding requests time to finish
                    // then we reset the counters
                    {
                        _azureSendThreadResetCounter = 0;
                        _azureSendThreads = 0;
                    }
                }
            }
            else
            {
#if MulticolorLed
                myMulticolorLedAsync.light("red", 200);
#endif
#if DebugPrint
                    Debug.Print("\r\nRow was discarded, sendInterval was not expired ");
#endif
            }
#if DebugPrint
                Debug.Print("\r\nRemaining Ram:" + remainingRam.ToString() + "\r\n");
#endif
                #endregion

            #region Prepare rebooting of the mainboard e.g. if not enough ram due to memory leak
            if (_willRebootAfterNextEvent)
            {
#if DebugPrint
                    Debug.Print("Board is going to reboot in 3000 ms\r\n");
#endif
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
            }
            if (willReboot)
            { _willRebootAfterNextEvent = true; }
            #endregion
#if DisplayN18
            displayN18.Clear();
            displayN18.Orientation = GTM.Module.DisplayModule.DisplayOrientation.Clockwise90Degrees;
            if (lastOutString != string.Empty)
            { displayN18.SimpleGraphics.DisplayText(lastOutString, RenderingFont, Gadgeteer.Color.Black, 1, 1); }
            lastOutString = outString.Substring(48, 8) + " " + outString.Substring(88, 8);
            displayN18.SimpleGraphics.DisplayText(lastOutString, RenderingFont, Gadgeteer.Color.Orange, 1, 1);
#endif
#if SD_Card_Logging
                source = new LogContent() { logOrigin = "Event: RF 433 Signal received", logReason = "n", logPosition = "End of method", logNumber = 1 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif

        }
        #endregion
   
        #region Event SolarPumpCurrentDataSensor_SignalReceived
        // This eventmanager is for the case when Continuous Sensordata from the Smartmeter (and Fritz!Dect) were sent
        static void mySolarPumpCurrentSensor_rfm69DataSensorSend(OnOffRfm69SensorMgr sender, OnOffRfm69SensorMgr.DataSensorEventArgs e)
        {
            
            string outString = string.Empty;
            bool forceSend = false;
            // The magic word EscapeTableLocation_03 makes, that not the tabelname from e is used, but from the const _tablePreFix_Definition_3
            string tablePreFix = (e.DestinationTable == "EscapeTableLocation_03") ? _tablePreFix_Definition_3 : e.DestinationTable;

            
            // get minimal and maximal gained Power by the the solar panel
            double dayMaxBefore = AzureSendManager._dayMax < 0 ? 0.00 : AzureSendManager._dayMax;
            double dayMinBefore = AzureSendManager._dayMin > 70 ? 0.00 : AzureSendManager._dayMin;

            //double decimalValue = (double)e.Val_1 / 100;

            // Get Power from Fritzbox
            string solarPower = fritz.getSwitchPower(FRITZ_DEVICE_AIN_01);

            double decimalValue = solarPower != null ? double.Parse(solarPower) / 10000 : InValidValue;                
            double logCurrent = ((decimalValue > 170) || (decimalValue < -40)) ? InValidValue : (decimalValue > 160) ? 160.0 : decimalValue;

            // Get Energy from Fritzbox
            string solarEnergy = fritz.getSwitchEnergy(FRITZ_DEVICE_AIN_01);

            double solarEnergy_decimal_value = solarEnergy != null ? double.Parse(solarEnergy) / 100 : InValidValue;


            double t4_decimal_value = (double)e.Val_2 / 100;      // measuredPower
            double t2_decimal_value = (t4_decimal_value > 6000) ? 60 : (t4_decimal_value / 100);

            double t5_decimal_value = (double)Reform_uint16_2_float32.Convert((UInt16)(e.Val_3 >> 16), (UInt16)(e.Val_3 & 0x0000FFFF));  // measuredWork 

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: RF 433 Signal received", logReason = "n", logPosition = "RF 433 event Start", logNumber = 1 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif

#if DebugPrint
            Debug.Print("Rfm69 event, Data: " + decimalValue.ToString("f2") + " Amps " + t4_decimal_value.ToString("f2") + " Watt " + t5_decimal_value.ToString("f2") + " KWh");
#endif

            activateWatchdogIfAllowedAndNotYetRunning();

            #region Preset some table parameters like Row Headers
            // Here we set some table parameters which where transmitted in the eventhandler and were set in the constructor of the RF_433_Receiver Class

            //_tablePreFix_Current = e.DestinationTable;

            _partitionKey_Current = e.SensorLabel;
            _location_Current = e.SensorLocation;
            _sensorValueHeader_Current = e.MeasuredQuantity;
            _socketSensorHeader_Current = "NU";
            #endregion


            DateTime timeOfThisEvent = DateTime.Now;
            AzureSendManager._timeOfLastSensorEvent = timeOfThisEvent;

            // Reset _sensorControlTimer, if the timer is not reset, the board will be rebooted
            _sensorControlTimer = new Timer(new TimerCallback(_sensorControlTimer_Tick), null, _sensorControlTimerInterval, _sensorControlTimerInterval);
            // when no sensor events occure in a certain timespan

            string switchMessage = "Switch Message Preset";
            string switchState = "???";
            string actCurrent = "???";

            #region Test if timeService is running. If not, try to initialize
            if (!timeServiceIsRunning)
            {
                if (DateTime.Now < new DateTime(2016, 7, 1))
                {
#if DebugPrint
                        Debug.Print("Going to set the time in rf_433_Receiver_SignalReceived event");
#endif
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
                    Thread.Sleep(200);
                }
                else
                {
                    timeServiceIsRunning = true;
                }
                if (!timeServiceIsRunning)
                {
#if DebugPrint
                        Debug.Print("Sending aborted since timeservice is not running");
#endif
                    return;
                }
            }
            #endregion

            #region Do some tests with RegEx to assure that proper content is transmitted to the Azure table

            RegexTest.ThrowIfNotValid(_tableRegex, new string[] { tablePreFix, _location_Current});
            RegexTest.ThrowIfNotValid(_columnRegex, new string[] { _sensorValueHeader_Current});

            #endregion


            #region After a reboot: Read the last stored entity from Azure to actualize the counters
            if (AzureSendManager._iteration == 0)    // The system has rebooted: We read the last entity from the Cloud
            {
                _counters = myAzureSendManager.ActualizeFromLastAzureRow(ref switchMessage);

                _azureSendErrors = _counters.AzureSendErrors > _azureSendErrors ? _counters.AzureSendErrors : _azureSendErrors;
                _azureSends = _counters.AzureSends > _azureSends ? _counters.AzureSends : _azureSends;
                _forcedReboots = _counters.ForcedReboots > _forcedReboots ? _counters.ForcedReboots : _forcedReboots;
                _badReboots = _counters.BadReboots > _badReboots ? _counters.BadReboots : _badReboots;
                

                forceSend = true;
                // actualize to consider the timedelay caused by reading from the cloud
                timeOfThisEvent = DateTime.Now;
                AzureSendManager._timeOfLastSensorEvent = timeOfThisEvent;
            }
            #endregion

            // when every sample value shall be sent to Azure, remove the outcomment of the next two lines
            //forceSend = true;                 
            //switchMessage = "Sending was forced by Program";              

            #region Check if we still have enough free ram (memory leak in https) and evtl. prepare for resetting the mainboard
            uint remainingRam = Debug.GC(false);            // Get remaining Ram because of the memory leak in https
            bool willReboot = (remainingRam < freeRamThreshold);     // If the ram is below this value, the Mainboard will reboot
            if (willReboot)
            {
                forceSend = true;
                switchMessage = "Going to reboot the Mainboard due to not enough free RAM";
            }
            #endregion

            DateTime copyTimeOfLastSend = AzureSendManager._timeOfLastSend;

            TimeSpan timeFromLastSend = timeOfThisEvent - copyTimeOfLastSend;

            double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);

            #region Set the partitionKey
            //string partitionKey = _partitionKey_Current;                    // Set Partition Key for Azure storage table
            string partitionKey = e.SensorLabel;                    // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                        // if wanted, augment with year and month (12 - month for right order)
            //{ partitionKey = partitionKey + DateTime.Now.ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.Month).ToString(), 2); }
            { partitionKey = partitionKey + DateTime.Now.AddMinutes(daylightCorrectOffset).ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.AddMinutes(daylightCorrectOffset).Month).ToString(), 2); }
            #endregion

            #region Regex test for proper content of the Message property in the Azure table
            // The regex test can be outcommented if the string is valid
            if (!_stringRegex.IsMatch(switchMessage))
            { throw new NotSupportedException("Some charcters [<>] may not be used in this string"); }
            #endregion


            #region If sendInterval has expired, write new sample value row to the buffer and start writing to Azure
            if ((timeFromLastSend > AzureSendManager._sendInterval) || forceSend)
            {
                #region actualize the values of minumum and maximum measurements of the day
            
                if (AzureSendManager._timeOfLastSend.AddMinutes(daylightCorrectOffset).Day == timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day)              
                {
                    // same day as event before
                    // RoSchmi
                    AzureSendManager._dayMaxWorkBefore = AzureSendManager._dayMaxWork;
                    AzureSendManager._dayMinWorkBefore = AzureSendManager._dayMinWork;
                    AzureSendManager._dayMaxSolarWorkBefore = AzureSendManager._dayMaxSolarWork;
                    AzureSendManager._dayMinSolarWorkBefore = AzureSendManager._dayMinSolarWork;

                    Debug.Print(AzureSendManager._dayMaxWork.ToString("F4"));
                    Debug.Print(AzureSendManager._dayMaxWorkBefore.ToString("F4"));


                    AzureSendManager._dayMaxWork = t5_decimal_value;     // measuredWork
                    if (AzureSendManager._dayMinWork < 0.1)
                    {
                        AzureSendManager._dayMinWorkBefore = AzureSendManager._dayMinWork;
                        AzureSendManager._dayMinWork = t5_decimal_value;  // measuredWork
                    }

                    AzureSendManager._dayMaxSolarWork = solarEnergy_decimal_value;     // measuredSolarWork
                    if (AzureSendManager._dayMinSolarWork < 0.1)
                    {
                        AzureSendManager._dayMinSolarWork = solarEnergy_decimal_value;  // measuredSolarWork
                    }


                    if ((decimalValue > AzureSendManager._dayMax) && (decimalValue < 70.0))
                    {
                        AzureSendManager._dayMax = decimalValue;
                    }
                    if ((decimalValue > -39.0) && ((decimalValue < AzureSendManager._dayMin)) || AzureSendManager._dayMin < 0.001)
                    {
                        AzureSendManager._dayMin = decimalValue;
                    }

                    

                }
                else
                {
                    // first event of a new day

                    // decimalValue is actually gained power of solar panel
                    if ((decimalValue > -39.0) && (decimalValue < 70.0))
                    {
                        AzureSendManager._dayMax = decimalValue;
                        AzureSendManager._dayMin = decimalValue;
                    }
                    AzureSendManager._dayMinWorkBefore = AzureSendManager._dayMinWork;
                    AzureSendManager._dayMinWork = t5_decimal_value;                        // measuredWork
                    AzureSendManager._dayMaxWorkBefore = AzureSendManager._dayMaxWork;
                    AzureSendManager._dayMaxWork = t5_decimal_value;                        // measuredWork

                    AzureSendManager._dayMinSolarWorkBefore = AzureSendManager._dayMinSolarWork;
                    AzureSendManager._dayMinSolarWork = solarEnergy_decimal_value;                        // measuredWork
                    AzureSendManager._dayMaxSolarWorkBefore = AzureSendManager._dayMaxSolarWork;
                    AzureSendManager._dayMaxSolarWork = solarEnergy_decimal_value;                        // measuredWork

                    

                }
                #endregion

                AzureSendManager._lastValue = decimalValue;

                for (int i = 0; i < 8; i++)
                {                  
                    _sensorValueArr_Out[i] = new SensorValue(AzureSendManager._timeOfLastSensorEvent, 0, 0, 0, 0, InValidValue, 999, 0x00, false);
                }
                

                _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble = t2_decimal_value;                 // T_2 : Power, limited to a max. Value

                //RoSchmi
                // T_1 : Solar Work of this day
                _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = ((AzureSendManager._dayMaxSolarWork - AzureSendManager._dayMinSolarWork) <= 0) ? 0.00 : AzureSendManager._dayMaxSolarWork - AzureSendManager._dayMinSolarWork;
               // _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble <  9 ? _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble * 10 : 90.00;    // set limit to 90 and change scale
               // _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble < 9 ? _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble * 0.5 : 90.00;    // set limit to 90 and change scale
               // Bugfix
                _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble < 50 ? _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble * 0.5 : 90.00;    // set limit to 90 and change scale


                // RoSchmi
                // T_3 : Work of this day
                _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble = ((AzureSendManager._dayMaxWork - AzureSendManager._dayMinWork) <= 0) ? 0.00 : AzureSendManager._dayMaxWork - AzureSendManager._dayMinWork;
                _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble = _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble < 18 ? _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble * 5 : 90.00;    // set limit to 90 and change scale


                _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble = t4_decimal_value;                // T_4 : Power
                _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble = t5_decimal_value;                // T_5 : Work
                _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble = AzureSendManager._dayMinWork;    // T_6 : Work at start of day

                double energyConsumptionLastDay = AzureSendManager._dayMinWorkBefore < 0.01 ? 0 : (AzureSendManager._dayMaxWorkBefore - AzureSendManager._dayMinWorkBefore) / 5.0;

                AzureSendManager._iteration++;
                       
                SampleValue theRow = new SampleValue(tablePreFix + DateTime.Now.Year, partitionKey, e.Timestamp, timeZoneOffset + (int)daylightCorrectOffset, logCurrent, AzureSendManager._dayMin, AzureSendManager._dayMax,
                   _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_1_Sel - 1].RandomId, _sensorValueArr_Out[Ch_1_Sel - 1].Hum, _sensorValueArr_Out[Ch_1_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_2_Sel - 1].RandomId, _sensorValueArr_Out[Ch_2_Sel - 1].Hum, _sensorValueArr_Out[Ch_2_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_3_Sel - 1].RandomId, _sensorValueArr_Out[Ch_3_Sel - 1].Hum, _sensorValueArr_Out[Ch_3_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_4_Sel - 1].RandomId, _sensorValueArr_Out[Ch_4_Sel - 1].Hum, _sensorValueArr_Out[Ch_4_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_5_Sel - 1].RandomId, _sensorValueArr_Out[Ch_5_Sel - 1].Hum, _sensorValueArr_Out[Ch_5_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_6_Sel - 1].RandomId, _sensorValueArr_Out[Ch_6_Sel - 1].Hum, _sensorValueArr_Out[Ch_6_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_7_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_7_Sel - 1].RandomId, _sensorValueArr_Out[Ch_7_Sel - 1].Hum, _sensorValueArr_Out[Ch_7_Sel - 1].BatteryIsLow,
                   _sensorValueArr_Out[Ch_8_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_8_Sel - 1].RandomId, _sensorValueArr_Out[Ch_8_Sel - 1].Hum, _sensorValueArr_Out[Ch_8_Sel - 1].BatteryIsLow,
                   actCurrent, switchState, _location_Current, timeFromLastSend, e.RepeatSend, e.RSSI, AzureSendManager._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, forceSend ? switchMessage : "");
                

                if (AzureSendManager._iteration == 1)
                {
                    if (timeFromLastSend < makeInvalidTimeSpan)   // after reboot for the first time take values which were read back from the Cloud
                    {
                        //theRow.T_0 = AzureSendManager._lastContent[Ch_1_Sel - 1];
                        //theRow.T_1 = AzureSendManager._lastContent[Ch_2_Sel - 1];
                        theRow.T_2 = AzureSendManager._lastContent[Ch_3_Sel - 1];
                        //theRow.T_3 = AzureSendManager._lastContent[Ch_4_Sel - 1];
                        //theRow.T_4 = AzureSendManager._lastContent[Ch_5_Sel - 1];
                        theRow.T_5 = AzureSendManager._lastContent[Ch_6_Sel - 1];
                        //theRow.T_6 = AzureSendManager._lastContent[Ch_7_Sel - 1];
                        //theRow.T_7 = AzureSendManager._lastContent[Ch_8_Sel - 1];
                    }
                    else
                    {
                        //theRow.T_0 = InValidValue;
                        //theRow.T_1 = InValidValue;
                        theRow.T_2 = InValidValue;
                        //theRow.T_3 = InValidValue;
                        //theRow.T_4 = InValidValue;
                        theRow.T_5 = InValidValue;
                        //theRow.T_6 = InValidValue;
                        //theRow.T_7 = InValidValue;
                    }
                }


                if (AzureSendManager.hasFreePlaces())
                {
                    AzureSendManager.EnqueueSampleValue(theRow);

                    // RoSchmi
                    //copyTimeOfLastSend = timeOfThisEvent;
                    AzureSendManager._timeOfLastSend = timeOfThisEvent;

                    //Debug.Print("\r\nRow was writen to the Buffer. Number of rows in the buffer = " + AzureSendManager.Count + ", still " + (AzureSendManager.capacity - AzureSendManager.Count).ToString() + " places free");
                }
                // optionally send message to Debug.Print  
                //SampleValue theReturn = AzureSendManager.PreViewNextSampleValue();
                //DateTime thatTime = theReturn.TimeOfSample;
                //double thatDouble = theReturn.TheSampleValue;
                //Debug.Print("The Temperature: " + thatDouble.ToString() + "  at: " + thatTime.ToString());

            #endregion


                #region ligth a multicolour led asynchronously to indicate action
#if MulticolorLed
                myMulticolorLedAsync.light("green", 3000);
#endif
                #endregion

                #region If sendInterval has expired, send contents of the buffer to Azure
                //if (_azureSendThreads == 0)
                if (_azureSendThreads == 0)
                {
                    lock (MainThreadLock)
                    {
                        _azureSendThreads++;
                    }
                    // RoSchmi
                    myAzureSendManager = new AzureSendManager(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, tablePreFix, _sensorValueHeader_Current, _socketSensorHeader_Current, caCerts, timeOfThisEvent, sendInterval_Current, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                    myAzureSendManager.AzureCommandSend += myAzureSendManager_AzureCommandSend;
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    _Print_Debug("\r\nRow was sent on its way to Azure");
                    myAzureSendManager.Start();

                    //RoSchmi
                    // if last send was yesterday: write 
                    var last = copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).AddDays(1).Day;

                    var theAct = timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day;

                    // if we have the first upload of a new day
                    if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).AddDays(1).Day == timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day)

                   //if (true)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            _sensorValueArr_Out[i] = new SensorValue(copyTimeOfLastSend, 0, 0, 0, 0, InValidValue, 999, 0x00, false);

                        }

                        //RoSchmi
                        // T_1 : Solar Work of this day
                        //_sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = ((AzureSendManager._dayMaxSolarWork - AzureSendManager._dayMinSolarWork) <= 0) ? 0.00 : AzureSendManager._dayMaxSolarWork - AzureSendManager._dayMinSolarWork;

                        _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = ((AzureSendManager._dayMaxSolarWorkBefore - AzureSendManager._dayMinSolarWorkBefore) <= 0) ? 0.00 : AzureSendManager._dayMaxSolarWorkBefore - AzureSendManager._dayMinSolarWorkBefore;
                       // _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble < 9 ? _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble * 10 : 90.00;    // set limit to 90 and change scale
                        _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble = _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble < 9 ? _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble * 0.5 : 90.00;    // set limit to 90 and change scale

                        


                        forceSend = true;


                        theRow = new SampleValue(tablePreFix + "Days" + DateTime.Now.Year, partitionKey, copyTimeOfLastSend.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset,
                            copyTimeOfLastSend, true)), timeZoneOffset + (int)daylightCorrectOffset, energyConsumptionLastDay, dayMinBefore, dayMaxBefore,
                           _sensorValueArr_Out[Ch_1_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_1_Sel - 1].RandomId, _sensorValueArr_Out[Ch_1_Sel - 1].Hum, _sensorValueArr_Out[Ch_1_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_2_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_2_Sel - 1].RandomId, _sensorValueArr_Out[Ch_2_Sel - 1].Hum, _sensorValueArr_Out[Ch_2_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_3_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_3_Sel - 1].RandomId, _sensorValueArr_Out[Ch_3_Sel - 1].Hum, _sensorValueArr_Out[Ch_3_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_4_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_4_Sel - 1].RandomId, _sensorValueArr_Out[Ch_4_Sel - 1].Hum, _sensorValueArr_Out[Ch_4_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_5_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_5_Sel - 1].RandomId, _sensorValueArr_Out[Ch_5_Sel - 1].Hum, _sensorValueArr_Out[Ch_5_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_6_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_6_Sel - 1].RandomId, _sensorValueArr_Out[Ch_6_Sel - 1].Hum, _sensorValueArr_Out[Ch_6_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_7_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_7_Sel - 1].RandomId, _sensorValueArr_Out[Ch_7_Sel - 1].Hum, _sensorValueArr_Out[Ch_7_Sel - 1].BatteryIsLow,
                           _sensorValueArr_Out[Ch_8_Sel - 1].TempDouble, _sensorValueArr_Out[Ch_8_Sel - 1].RandomId, _sensorValueArr_Out[Ch_8_Sel - 1].Hum, _sensorValueArr_Out[Ch_8_Sel - 1].BatteryIsLow,
                           " ", " ", _location_Current, new TimeSpan(0), e.RepeatSend, e.RSSI, AzureSendManager._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, forceSend ? switchMessage : "");


                        waitForCurrentCallback.Reset();
                        waitForCurrentCallback.WaitOne(50000, true);

                        Thread.Sleep(5000); // Wait additional 5 sec for last thread AzureSendManager Thread to finish
                        AzureSendManager.EnqueueSampleValue(theRow);
                        
                        // RoSchmi
                        //myAzureSendManager = new AzureSendManager(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable + "Days", "Work", _socketSensorHeader_Current, caCerts, timeOfThisEvent, sendInterval_Current, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                        myAzureSendManager = new AzureSendManager(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable + "Days", _sensorValueHeader_Current, _socketSensorHeader_Current, caCerts, timeOfThisEvent, sendInterval_Current, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                        
                        
                        myAzureSendManager.AzureCommandSend += myAzureSendManager_AzureCommandSend;
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        //_Print_Debug("\r\nRow was sent on its way to Azure");
#if DebugPrint
                            Debug.Print("\r\nLast Row of day was sent on its way to Azure");
#endif

                        myAzureSendManager.Start();

                        
                    }
                }
                else
                {
                    _azureSendThreadResetCounter++;
#if DebugPrint
                        Debug.Print("_azureSendThreadResetCounter = " + _azureSendThreadResetCounter.ToString());
#endif
                    if (_azureSendThreadResetCounter > 5)   // when _azureSendThread != 0 we write the next 5 rows coming through sensor events only to the buffer
                    // this should give outstanding requests time to finish
                    // then we reset the counters
                    {
                        _azureSendThreadResetCounter = 0;
                        _azureSendThreads = 0;
                    }
                }
            }
            else
            {
#if MulticolorLed
                myMulticolorLedAsync.light("red", 200);
#endif
#if DebugPrint
                    Debug.Print("\r\nRow was discarded, sendInterval was not expired ");
#endif
            }
#if DebugPrint
                Debug.Print("\r\nRemaining Ram:" + remainingRam.ToString() + "\r\n");
#endif
                #endregion

            #region Prepare rebooting of the mainboard e.g. if not enough ram due to memory leak
            if (_willRebootAfterNextEvent)
            {
#if DebugPrint
                    Debug.Print("Board is going to reboot in 3000 ms\r\n");
#endif
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
            }
            if (willReboot)
            { _willRebootAfterNextEvent = true; }
            #endregion
#if DisplayN18
            displayN18.Clear();
            displayN18.Orientation = GTM.Module.DisplayModule.DisplayOrientation.Clockwise90Degrees;
            if (lastOutString != string.Empty)
            { displayN18.SimpleGraphics.DisplayText(lastOutString, RenderingFont, Gadgeteer.Color.Black, 1, 1); }
            lastOutString = outString.Substring(48, 8) + " " + outString.Substring(88, 8);
            displayN18.SimpleGraphics.DisplayText(lastOutString, RenderingFont, Gadgeteer.Color.Orange, 1, 1);
#endif
#if SD_Card_Logging
                source = new LogContent() { logOrigin = "Event: RF 433 Signal received", logReason = "n", logPosition = "End of method", logNumber = 1 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif

        }
        #endregion
        
        #region Event SolarPumpOnOffSensor Signal received

        static void mySolarPumpCurrentSensor_rfm69OnOffSensorSend(OnOffRfm69SensorMgr sender, OnOffBaseSensorMgr.OnOffSensorEventArgs e)
        {
            
            string switchMessage = " ";
            bool forceSend = true;

            #region Test if timeService is running. If not, try to initialize
            if (!timeServiceIsRunning)
            {
                if (DateTime.Now < new DateTime(2016, 7, 1))
                {
#if DebugPrint
                        Debug.Print("Going to set the time in SignalReceived event");
#endif
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
                    Thread.Sleep(200);
                }
                else
                {
                    timeServiceIsRunning = true;
                }
                if (!timeServiceIsRunning)
                {
#if DebugPrint
                        Debug.Print("Sending aborted since timeservice is not running");
#endif
                    return;
                }
            }
            #endregion

            #region Do some tests with RegEx to assure that proper content is transmitted to the Azure table

            RegexTest.ThrowIfNotValid(_tableRegex, new string[] { e.DestinationTable, e.SensorLocation, e.MeasuredQuantity });
            RegexTest.ThrowIfNotValid(_columnRegex, new string[] { _sensorValueHeader_Current });

            #endregion


            DateTime timeOfThisEvent = DateTime.Now;     // Works with no daylightsavingtime correction
            AzureSendManager_Solar._timeOfLastSensorEvent = timeOfThisEvent;   
            // when no sensor events occure in a certain timespan


            #region After a reboot: Read the last stored entity from Azure to actualize the counters and minimum and maximum values of the day

            
            // We are in Event SolarPumpOnOffSensor Signal received

            if (AzureSendManager_Solar._iteration == 0)
            {

                _counters = myAzureSendManager_Solar.ActualizeFromLastAzureRow(ref switchMessage);

                _azureSendErrors = _counters.AzureSendErrors > _azureSendErrors ? _counters.AzureSendErrors : _azureSendErrors;
                _azureSends = _counters.AzureSends > _azureSends ? _counters.AzureSends : _azureSends;
                _forcedReboots = _counters.ForcedReboots > _forcedReboots ? _counters.ForcedReboots : _forcedReboots;
                _badReboots = _counters.BadReboots > _badReboots ? _counters.BadReboots : _badReboots;

                forceSend = true;
                // actualize to consider the timedelay caused by reading from the cloud
                timeOfThisEvent = DateTime.Now;
                AzureSendManager_Solar._timeOfLastSensorEvent = timeOfThisEvent;
            }

            #endregion

        
            #region Check if we still have enough free ram (memory leak in https) and evtl. prepare for resetting the mainboard
            uint remainingRam = Debug.GC(false);            // Get remaining Ram because of the memory leak in https
            bool willReboot = (remainingRam < freeRamThreshold);     // If the ram is below this value, the Mainboard will reboot
            if (willReboot)
            {
                forceSend = true;
                switchMessage = "Going to reboot due to not enough free RAM";
            }
            #endregion

            DateTime copyTimeOfLastSend = AzureSendManager_Solar._timeOfLastSend;

            TimeSpan timeFromLastSend = timeOfThisEvent - copyTimeOfLastSend;

            double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);

            #region Set the partitionKey
            string partitionKey = e.SensorLabel;                    // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                        // if wanted, augment with year and month (12 - month for right order)
            //{ partitionKey = partitionKey + DateTime.Now.ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.Month).ToString(), 2); }
            { partitionKey = partitionKey + DateTime.Now.AddMinutes(daylightCorrectOffset).ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.AddMinutes(daylightCorrectOffset).Month).ToString(), 2); }
            #endregion


            #region Regex test for proper content of the Message property in the Azure table
            // The regex test can be outcommented if the string is valid
            if (!_stringRegex.IsMatch(switchMessage))
            { throw new NotSupportedException("Some charcters [<>] may not be used in this string"); }
            #endregion

            #region If sendInterval has expired, write new sample value row to the buffer and start writing to Azure

            //double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);
            if ((timeFromLastSend > AzureSendManager_Solar._sendInterval) || forceSend)
            {
                if (e.ActState == true)       // if switched off
                {
                    if (AzureSendManager_Solar._iteration != 0)
                    {
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day == e.Timestamp.Day)
                        {
                            if (e.OldState == false)  // only if it was on before
                            {
                                AzureSendManager_Solar._onTimeDay += timeFromLastSend;
                                AzureSendManager_Solar._onTimeWeek += timeFromLastSend;
                                AzureSendManager_Solar._onTimeMonth += timeFromLastSend;
                                AzureSendManager_Solar._onTimeYear += timeFromLastSend;
                                AzureSendManager_Solar._CD++;
                                AzureSendManager_Solar._CW++;
                                AzureSendManager_Solar._CM++;
                                AzureSendManager_Solar._CY++;
                            }
                        }
                        else
                        {
                            AzureSendManager_Solar._onTimeDay = new TimeSpan(0);
                            AzureSendManager_Solar._CD = 0;
                            if (!((copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).DayOfWeek == DayOfWeek.Sunday) && (e.Timestamp.DayOfWeek == DayOfWeek.Monday)))
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Solar._onTimeWeek += timeFromLastSend;
                                    AzureSendManager_Solar._CW++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Solar._onTimeWeek = new TimeSpan(0);
                                AzureSendManager_Solar._CW = 0;

                            }
                            if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Month == e.Timestamp.Month)
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Solar._onTimeMonth += timeFromLastSend;
                                    AzureSendManager_Solar._CM++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Solar._onTimeMonth = new TimeSpan(0);
                                AzureSendManager_Solar._CM = 0;
                            }
                            if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Year == e.Timestamp.Year)
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Solar._onTimeYear += timeFromLastSend;
                                    AzureSendManager_Solar._CY++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Solar._onTimeYear = new TimeSpan(0);
                                AzureSendManager_Solar._CY = 0;
                            }
                        }
                    }
                }
                else         // if switched on
                {
                    if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day != e.Timestamp.Day)
                    {
                        AzureSendManager_Solar._onTimeDay = new TimeSpan(0);
                        AzureSendManager_Solar._CD = 0;
                        if ((copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).DayOfWeek == DayOfWeek.Sunday) && (e.Timestamp.DayOfWeek == DayOfWeek.Monday))
                        {
                            AzureSendManager_Solar._onTimeWeek = new TimeSpan(0);
                            AzureSendManager_Solar._CW = 0;
                        }
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Month != e.Timestamp.Month)
                        {
                            AzureSendManager_Solar._onTimeMonth = new TimeSpan(0);
                            AzureSendManager_Solar._CM = 0;
                        }
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Year != e.Timestamp.Year)
                        {
                            AzureSendManager_Solar._onTimeYear = new TimeSpan(0);
                            AzureSendManager_Solar._CY = 0;
                        }
                    }
                }

                //_iteration++;
                AzureSendManager_Solar._iteration++;

                
                float work = 0;

                //float work = Reform_uint16_2_float32.Convert(e.Val_3_High, e.Val_3_Low);
                OnOffSample theRow = new OnOffSample(partitionKey, e.Timestamp, timeZoneOffset + (int)daylightCorrectOffset, e.ActState ? "Off" : "On", e.OldState ? "Off" : "On", e.SensorLocation, timeFromLastSend, AzureSendManager_Solar._onTimeDay, AzureSendManager_Solar._CD, AzureSendManager_Solar._onTimeWeek, AzureSendManager_Solar._CW, AzureSendManager_Solar._onTimeMonth, AzureSendManager_Solar._CM, AzureSendManager_Solar._onTimeYear, AzureSendManager_Solar._CY, AzureSendManager_Solar._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, switchMessage, e.RepeatSend, "0", "0", "0");

                
                if (AzureSendManager_Solar.hasFreePlaces())
                {
                    AzureSendManager_Solar.EnqueueSampleValue(theRow);
                    copyTimeOfLastSend = timeOfThisEvent;
                    AzureSendManager_Solar._timeOfLastSend = timeOfThisEvent;

                    //Debug.Print("\r\nRow was writen to the Buffer. Number of rows in the buffer = " + AzureSendManager.Count + ", still " + (AzureSendManager.capacity - AzureSendManager.Count).ToString() + " places free");
                }
                

            #endregion

                #region If sendInterval has expired, send contents of the buffer to Azure
                if (_azureSendThreads < 8)
                {
                    lock (MainThreadLock)
                    {
                        _azureSendThreads++;
                    }

                    myAzureSendManager_Solar = new AzureSendManager_Solar(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable, e.MeasuredQuantity, "", caCerts, copyTimeOfLastSend, sendInterval_Burner, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                    
                    myAzureSendManager_Solar.AzureCommandSend += myAzureSendManager_Solar_AzureCommandSend;
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    //_Print_Debug("\r\nRow was sent on its way to Azure");
                    // Debug.Print("\r\nRfm69 event, State: " + e.ActState.ToString() + ", RepeatCount: " + e.RepeatSend.ToString());
                    Debug.Print("\r\nRow was sent on its way to Azure (Solar)");
                    myAzureSendManager_Solar.Start();
                    // RoSchmi
                    //Thread.Sleep(40000);
                    Thread.Sleep(5000);


                    if (e.LastOfDay)   // Write the last méssage of the day to a separate table where the TableName is augmented with "Days" (eg. TestDays2018)                  
                    {
                        theRow = new OnOffSample(partitionKey, e.Timestamp, timeZoneOffset + (int)daylightCorrectOffset, e.ActState ? "Off" : "On", e.OldState ? "Off" : "On", e.SensorLocation, timeFromLastSend, AzureSendManager_Solar._onTimeDay, AzureSendManager_Solar._CD, AzureSendManager_Solar._onTimeWeek, AzureSendManager_Solar._CW, AzureSendManager_Solar._onTimeMonth, AzureSendManager_Solar._CM, AzureSendManager_Solar._onTimeYear, AzureSendManager_Solar._CY, AzureSendManager_Solar._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, switchMessage, e.RepeatSend, e.Val_1.ToString(), e.Val_2.ToString(), work.ToString());
                        AzureSendManager_Solar.EnqueueSampleValue(theRow);
                        myAzureSendManager_Solar = new AzureSendManager_Solar(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable + "Days", e.MeasuredQuantity, "", caCerts, copyTimeOfLastSend, sendInterval_Burner, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                        myAzureSendManager_Solar.AzureCommandSend += myAzureSendManager_Solar_AzureCommandSend;
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        //_Print_Debug("\r\nRow was sent on its way to Azure");
                        //Debug.Print("\r\nLast Row of day was sent on its way to Azure");
                        myAzureSendManager_Solar.Start();
                    }

                }
                else
                {
                    _azureSendThreadResetCounter++;

                    if (_azureSendThreadResetCounter > 5)   // when _azureSendThread > 8 we write the next 5 rows coming through sensor events only to the buffer
                    // this should give outstanding requests time to finish
                    // then we reset the counters
                    {
                        _azureSendThreadResetCounter = 0;
                        _azureSendThreads = 0;
                    }

                }
                #endregion


                //Debug.Print("Burner Sensor sent actstate = " + e.ActState + " oldState = " + e.OldState + " at " + e.Timestamp);
            }
            else
            {
#if DebugPrint
                    Debug.Print("\r\nRow was discarded, sendInterval was not expired ");
#endif
            }

            #region Prepare rebooting of the mainboard e.g. if not enough ram due to memory leak
            if (_willRebootAfterNextEvent)
            {
#if DebugPrint
                    Debug.Print("Board is going to reboot in 3000 ms\r\n");
#endif
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
            }
            if (willReboot)
            { _willRebootAfterNextEvent = true; }
            #endregion

        }

#endregion
      
        #region Event BoilerHeizungSensor Signal received   
        static void myStoragePumpSensor_currentSensorSend(OnOffAnalogSensorMgr sender, OnOffAnalogSensorMgr.OnOffSensorEventArgs e)
        {
            string switchMessage = " ";
            bool forceSend = true;

            #region Test if timeService is running. If not, try to initialize
            if (!timeServiceIsRunning)
            {
                if (DateTime.Now < new DateTime(2016, 7, 1))
                {
#if DebugPrint
                    Debug.Print("Going to set the time in SignalReceived event");
#endif
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
                    Thread.Sleep(200);
                }
                else
                {
                    timeServiceIsRunning = true;
                }
                if (!timeServiceIsRunning)
                {
#if DebugPrint
                    Debug.Print("Sending aborted since timeservice is not running");
#endif
                    return;
                }
            }
            #endregion

            #region Do some tests with RegEx to assure that proper content is transmitted to the Azure table

                RegexTest.ThrowIfNotValid(_tableRegex, new string[] { e.DestinationTable, e.SensorLocation});
                RegexTest.ThrowIfNotValid(_columnRegex, new string[] {e.MeasuredQuantity });

            #endregion
          
                
            DateTime timeOfThisEvent = DateTime.Now;     // Works with no daylightsavingtime correction

            
            AzureSendManager_Solar._timeOfLastSensorEvent = timeOfThisEvent;    


            #region After a reboot: Read the last stored entity from Azure to actualize the counters and minimum and maximum values of the day

            if (AzureSendManager_Boiler._iteration == 0)
            {

                _counters = myAzureSendManager_Boiler.ActualizeFromLastAzureRow(ref switchMessage);

                _azureSendErrors = _counters.AzureSendErrors > _azureSendErrors ? _counters.AzureSendErrors : _azureSendErrors;
                _azureSends = _counters.AzureSends > _azureSends ? _counters.AzureSends : _azureSends;
                _forcedReboots = _counters.ForcedReboots > _forcedReboots ? _counters.ForcedReboots : _forcedReboots;
                _badReboots = _counters.BadReboots > _badReboots ? _counters.BadReboots : _badReboots;

                forceSend = true;
                // actualize to consider the timedelay caused by reading from the cloud
                timeOfThisEvent = DateTime.Now;
                AzureSendManager_Boiler._timeOfLastSensorEvent = timeOfThisEvent;
            }
        
            #endregion          

            #region Check if we still have enough free ram (memory leak in https) and evtl. prepare for resetting the mainboard
            uint remainingRam = Debug.GC(false);            // Get remaining Ram because of the memory leak in https
            bool willReboot = (remainingRam < freeRamThreshold);     // If the ram is below this value, the Mainboard will reboot
            if (willReboot)
            {
                forceSend = true;
                switchMessage = "Going to reboot due to not enough free RAM";
            }
            #endregion

            DateTime copyTimeOfLastSend = AzureSendManager_Boiler._timeOfLastSend;

            TimeSpan timeFromLastSend = timeOfThisEvent - copyTimeOfLastSend;

            double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);

            #region Set the partitionKey
            
            string partitionKey = e.SensorLabel;                    // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                        // if wanted, augment with year and month (12 - month for right order)
                //{ partitionKey = partitionKey + DateTime.Now.ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.Month).ToString(), 2); }
                { partitionKey = partitionKey + DateTime.Now.AddMinutes(daylightCorrectOffset).ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.AddMinutes(daylightCorrectOffset).Month).ToString(), 2); }
            #endregion


            #region Regex test for proper content of the Message property in the Azure table
                // The regex test can be outcommented if the string is valid
            if (!_stringRegex.IsMatch(switchMessage))
            { throw new NotSupportedException("Some charcters [<>] may not be used in this string"); }
            #endregion

            #region If sendInterval has expired, write new sample value row to the buffer and start writing to Azure

            // double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);
            if ((timeFromLastSend > AzureSendManager_Boiler._sendInterval) || forceSend)
            {          
                if (e.ActState == true)       // if switched off
                {
                    if (AzureSendManager_Boiler._iteration != 0)
                    {
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day == e.Timestamp.Day)
                        {
                            if (e.OldState == false)  // only if it was on before
                            {
                                AzureSendManager_Boiler._onTimeDay += timeFromLastSend;
                                AzureSendManager_Boiler._onTimeWeek += timeFromLastSend;
                                AzureSendManager_Boiler._onTimeMonth += timeFromLastSend;
                                AzureSendManager_Boiler._onTimeYear += timeFromLastSend;
                                AzureSendManager_Boiler._CD++;
                                AzureSendManager_Boiler._CW++;
                                AzureSendManager_Boiler._CM++;
                                AzureSendManager_Boiler._CY++;
                            }
                        }
                        else
                        {
                            AzureSendManager_Boiler._onTimeDay = new TimeSpan(0);
                            AzureSendManager_Boiler._CD = 0;
                            if (!((copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).DayOfWeek == DayOfWeek.Sunday) && (e.Timestamp.DayOfWeek == DayOfWeek.Monday)))
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Boiler._onTimeWeek += timeFromLastSend;
                                    AzureSendManager_Boiler._CW++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Boiler._onTimeWeek = new TimeSpan(0);
                                AzureSendManager_Boiler._CW = 0;

                            }
                            if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Month == e.Timestamp.Month)
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Boiler._onTimeMonth += timeFromLastSend;
                                    AzureSendManager_Boiler._CM++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Boiler._onTimeMonth = new TimeSpan(0);
                                AzureSendManager_Boiler._CM = 0;
                            }
                            if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Year == e.Timestamp.Year)
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Boiler._onTimeYear += timeFromLastSend;
                                    AzureSendManager_Boiler._CY++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Boiler._onTimeYear = new TimeSpan(0);
                                AzureSendManager_Boiler._CY = 0;
                            }
                        }
                    }
                }
                else         // if switched on
                {
                    if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day != e.Timestamp.Day)
                    {
                        AzureSendManager_Boiler._onTimeDay = new TimeSpan(0);
                        AzureSendManager_Boiler._CD = 0;
                        if ((copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).DayOfWeek == DayOfWeek.Sunday) && (e.Timestamp.DayOfWeek == DayOfWeek.Monday))
                        {
                            AzureSendManager_Boiler._onTimeWeek = new TimeSpan(0);
                            AzureSendManager_Boiler._CW = 0;
                        }
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Month != e.Timestamp.Month)
                        {
                            AzureSendManager_Boiler._onTimeMonth = new TimeSpan(0);
                            AzureSendManager_Boiler._CM = 0;
                        }
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Year != e.Timestamp.Year)
                        {
                            AzureSendManager_Boiler._onTimeYear = new TimeSpan(0);
                            AzureSendManager_Boiler._CY = 0;
                        }
                    }
                }

                AzureSendManager_Boiler._iteration++;

                OnOffSample theRow = new OnOffSample(partitionKey, e.Timestamp, timeZoneOffset + (int)daylightCorrectOffset, e.ActState ? "Off" : "On", e.OldState ? "Off" : "On", e.SensorLocation, timeFromLastSend, AzureSendManager_Boiler._onTimeDay, AzureSendManager_Boiler._CD, AzureSendManager_Boiler._onTimeWeek, AzureSendManager_Boiler._CW, AzureSendManager_Boiler._onTimeMonth, AzureSendManager_Boiler._CM, AzureSendManager_Boiler._onTimeYear, AzureSendManager_Boiler._CY, AzureSendManager_Boiler._iteration, 1, _forcedReboots, _badReboots, _azureSendErrors, '.', forceSend, switchMessage);
                
                if (AzureSendManager_Boiler.hasFreePlaces())
                {
                    AzureSendManager_Boiler.EnqueueSampleValue(theRow);
                    copyTimeOfLastSend = timeOfThisEvent;
                    AzureSendManager_Boiler._timeOfLastSend = timeOfThisEvent;
                    //Debug.Print("\r\nRow was writen to the Buffer. Number of rows in the buffer = " + AzureSendManager.Count + ", still " + (AzureSendManager.capacity - AzureSendManager.Count).ToString() + " places free");
                }

            #endregion

            #region If sendInterval has expired, send contents of the buffer to Azure
            if (_azureSendThreads < 8)
            {
                lock (MainThreadLock)
                {
                    _azureSendThreads++;
                }
                myAzureSendManager_Boiler = new AzureSendManager_Boiler(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable, e.MeasuredQuantity, "", caCerts, copyTimeOfLastSend, sendInterval_Boiler, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                myAzureSendManager_Boiler.AzureCommandSend += myAzureSendManager_Boiler_AzureCommandSend;
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };
                //_Print_Debug("\r\nRow was sent on its way to Azure");
                Debug.Print("\r\nRow was sent on its way to Azure (Boiler)");
                myAzureSendManager_Boiler.Start();

                if (e.LastOfDay)   // Write the last méssage of the day to a separate table where the TableName is augmented with "Days" (eg. TestDays2018)                  
                {
                    myAzureSendManager_Boiler = new AzureSendManager_Boiler(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable + "Days", e.MeasuredQuantity, "", caCerts, copyTimeOfLastSend, sendInterval_Boiler, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                        myAzureSendManager_Boiler.AzureCommandSend +=myAzureSendManager_Boiler_AzureCommandSend;
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        //_Print_Debug("\r\nRow was sent on its way to Azure");
                        Debug.Print("\r\nLast Row of day was sent on its way to Azure");
                        myAzureSendManager_Boiler.Start();
                    }

            }
            else
            {
                _azureSendThreadResetCounter++;

                if (_azureSendThreadResetCounter > 5)   // when _azureSendThread > 8 we write the next 5 rows coming through sensor events only to the buffer
                // this should give outstanding requests time to finish
                // then we reset the counters
                {
                    _azureSendThreadResetCounter = 0;
                    _azureSendThreads = 0;
                }

            }
            #endregion


                //Debug.Print("Burner Sensor sent actstate = " + e.ActState + " oldState = " + e.OldState + " at " + e.Timestamp);
            }
            else
            {
#if DebugPrint
                    Debug.Print("\r\nRow was discarded, sendInterval was not expired ");
#endif
            }

            #region Prepare rebooting of the mainboard e.g. if not enough ram due to memory leak
            if (_willRebootAfterNextEvent)
            {
#if DebugPrint
                    Debug.Print("Board is going to reboot in 3000 ms\r\n");
#endif
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
            }
            if (willReboot)
            { _willRebootAfterNextEvent = true; }
            #endregion


        }
        #endregion
    
        #region Event BurnerSensor Signal received      
        static void myBurnerSensor_digitalOnOffSensorSend(OnOffDigitalSensorMgr sender, OnOffBaseSensorMgr.OnOffSensorEventArgs e)
        {                    
            string switchMessage = " ";
            bool forceSend = true;
      
                #region Test if timeService is running. If not, try to initialize
                if (!timeServiceIsRunning)
                {
                    if (DateTime.Now < new DateTime(2016, 7, 1))
                    {
#if DebugPrint
                        Debug.Print("Going to set the time in SignalReceived event");
#endif
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        SetTime(timeZoneOffset, TimeServer_1, TimeServer_2);
                        Thread.Sleep(200);
                    }
                    else
                    {
                        timeServiceIsRunning = true;
                    }
                    if (!timeServiceIsRunning)
                    {
#if DebugPrint
                        Debug.Print("Sending aborted since timeservice is not running");
#endif
                        return;
                    }
                }
                #endregion

                #region Do some tests with RegEx to assure that proper content is transmitted to the Azure table

                RegexTest.ThrowIfNotValid(_tableRegex, new string[] { e.DestinationTable, e.SensorLocation, e.MeasuredQuantity });
                RegexTest.ThrowIfNotValid(_columnRegex, new string[] { e.MeasuredQuantity });

                #endregion



                
                DateTime timeOfThisEvent = DateTime.Now;     // Works with no daylightsavingtime correction
                AzureSendManager_Burner._timeOfLastSensorEvent = timeOfThisEvent;   


                #region After a reboot: Read the last stored entity from Azure to actualize the counters and minimum and maximum values of the day
                

                if (AzureSendManager_Burner._iteration == 0)
                {
                    
                    _counters =  myAzureSendManager_Burner.ActualizeFromLastAzureRow(ref switchMessage);

                    _azureSendErrors = _counters.AzureSendErrors > _azureSendErrors ? _counters.AzureSendErrors : _azureSendErrors;
                    _azureSends = _counters.AzureSends > _azureSends ? _counters.AzureSends : _azureSends;
                    _forcedReboots = _counters.ForcedReboots > _forcedReboots ? _counters.ForcedReboots : _forcedReboots;
                    _badReboots = _counters.BadReboots > _badReboots ? _counters.BadReboots : _badReboots;  
                    
                    forceSend = true;
                    // actualize to consider the timedelay caused by reading from the cloud
                    timeOfThisEvent = DateTime.Now;
                    AzureSendManager_Burner._timeOfLastSensorEvent = timeOfThisEvent; 
                }
                
                #endregion


           

            #region Check if we still have enough free ram (memory leak in https) and evtl. prepare for resetting the mainboard
            uint remainingRam = Debug.GC(false);            // Get remaining Ram because of the memory leak in https
            bool willReboot = (remainingRam < freeRamThreshold);     // If the ram is below this value, the Mainboard will reboot
            if (willReboot)
            {
                 forceSend = true;
                 switchMessage = "Going to reboot due to not enough free RAM";
            }
            #endregion
           
            DateTime copyTimeOfLastSend = AzureSendManager_Burner._timeOfLastSend;

            TimeSpan timeFromLastSend = timeOfThisEvent - copyTimeOfLastSend;

            double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);


            #region Set the partitionKey
            string partitionKey = e.SensorLabel;                    // Set Partition Key for Azure storage table
            if (augmentPartitionKey == true)                        // if wanted, augment with year and month (12 - month for right order)
                //{ partitionKey = partitionKey + DateTime.Now.ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.Month).ToString(), 2); }
            { partitionKey = partitionKey + DateTime.Now.AddMinutes(daylightCorrectOffset).ToString("yyyy") + "-" + X_Stellig.Zahl((12 - DateTime.Now.AddMinutes(daylightCorrectOffset).Month).ToString(), 2); }
            #endregion

            

            #region Regex test for proper content of the Message property in the Azure table
                // The regex test can be outcommented if the string is valid
            if (!_stringRegex.IsMatch(switchMessage))
            { throw new NotSupportedException("Some charcters [<>] may not be used in this string"); }
            #endregion

            #region If sendInterval has expired, write new sample value row to the buffer and start writing to Azure

            //double daylightCorrectOffset = DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, DateTime.Now, true);
            if ((timeFromLastSend > AzureSendManager_Burner._sendInterval) || forceSend)
            {
                #region actualize the values of minumum and maximum measurements of the day
                
                
                if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day == timeOfThisEvent.AddMinutes(daylightCorrectOffset).Day)
                {
                    /*
                    if ((decimalValue > _dayMax) && (decimalValue < 70.0))
                    {  
                        _dayMax = decimalValue;
                    }
                    if ((decimalValue > -39.0) && (decimalValue < _dayMin))
                    {
                        _dayMin = decimalValue;
                    }
                    */
                }
                else
                {
                    /*
                    if ((decimalValue > -39.0) && (decimalValue < 70.0))
                        _dayMax = decimalValue;
                    _dayMin = decimalValue;
                    */
                }
                #endregion

                

                if (e.ActState == true)       // if switched off
                {
                    if (AzureSendManager_Burner._iteration != 0)
                    {
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day == e.Timestamp.Day)
                        {
                            if (e.OldState == false)  // only if it was on before
                            {
                                AzureSendManager_Burner._onTimeDay += timeFromLastSend;
                                AzureSendManager_Burner._onTimeWeek += timeFromLastSend;
                                AzureSendManager_Burner._onTimeMonth += timeFromLastSend;
                                AzureSendManager_Burner._onTimeYear += timeFromLastSend;                              
                                AzureSendManager_Burner._CD++;                               
                                AzureSendManager_Burner._CW++;
                                AzureSendManager_Burner._CM++;
                                AzureSendManager_Burner._CY++;
                            }
                        }
                        else
                        {
                            AzureSendManager_Burner._onTimeDay = new TimeSpan(0);
                            AzureSendManager_Burner._CD = 0;
                            if (!((copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).DayOfWeek == DayOfWeek.Sunday) && (e.Timestamp.DayOfWeek == DayOfWeek.Monday)))
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Burner._onTimeWeek += timeFromLastSend;
                                    AzureSendManager_Burner._CW++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Burner._onTimeWeek = new TimeSpan(0);
                                AzureSendManager_Burner._CW = 0;

                            }
                            if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Month == e.Timestamp.Month)
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Burner._onTimeMonth += timeFromLastSend;
                                    AzureSendManager_Burner._CM++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Burner._onTimeMonth = new TimeSpan(0);
                                AzureSendManager_Burner._CM = 0;
                            }
                            if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Year == e.Timestamp.Year)
                            {
                                if (e.OldState == false)  // only if it was on before
                                {
                                    AzureSendManager_Burner._onTimeYear += timeFromLastSend;
                                    AzureSendManager_Burner._CY++;
                                }
                            }
                            else
                            {
                                AzureSendManager_Burner._onTimeYear = new TimeSpan(0);
                                AzureSendManager_Burner._CY = 0;
                            }
                        }
                    }
                }
                else         // if switched on
                {
                    if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Day != e.Timestamp.Day)
                    {
                        AzureSendManager_Burner._onTimeDay = new TimeSpan(0);
                        AzureSendManager_Burner._CD = 0;
                        if ((copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).DayOfWeek == DayOfWeek.Sunday) && (e.Timestamp.DayOfWeek == DayOfWeek.Monday))
                        {
                            AzureSendManager_Burner._onTimeWeek = new TimeSpan(0);
                            AzureSendManager_Burner._CW = 0;
                        }
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Month != e.Timestamp.Month)
                        {
                            AzureSendManager_Burner._onTimeMonth = new TimeSpan(0);
                            AzureSendManager_Burner._CM = 0;
                        }
                        if (copyTimeOfLastSend.AddMinutes(daylightCorrectOffset).Year != e.Timestamp.Year)
                        {
                            AzureSendManager_Burner._onTimeYear = new TimeSpan(0);
                            AzureSendManager_Burner._CY = 0;
                        }
                    }
                }

                //_iteration++;
                AzureSendManager_Burner._iteration++;


               // copyTimeOfLastSend.AddMinutes(RoSchmi.DayLihtSavingTime.DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, 
                              //  copyTimeOfLastSend, true))

                OnOffSample theRow = new OnOffSample(partitionKey, e.Timestamp, timeZoneOffset + (int)daylightCorrectOffset, e.ActState ? "Off" : "On", e.OldState ? "Off" : "On", e.SensorLocation, timeFromLastSend, AzureSendManager_Burner._onTimeDay, AzureSendManager_Burner._CD, AzureSendManager_Burner._onTimeWeek, AzureSendManager_Burner._CW, AzureSendManager_Burner._onTimeMonth, AzureSendManager_Burner._CM, AzureSendManager_Burner._onTimeYear, AzureSendManager_Burner._CY, AzureSendManager_Burner._iteration, remainingRam, _forcedReboots, _badReboots, _azureSendErrors, willReboot ? 'X' : '.', forceSend, switchMessage);              
                
                if (AzureSendManager_Burner.hasFreePlaces())
                {
                    AzureSendManager_Burner.EnqueueSampleValue(theRow);
                    copyTimeOfLastSend = timeOfThisEvent;
                    AzureSendManager_Burner._timeOfLastSend = timeOfThisEvent;

                    //Debug.Print("\r\nRow was writen to the Buffer. Number of rows in the buffer = " + AzureSendManager.Count + ", still " + (AzureSendManager.capacity - AzureSendManager.Count).ToString() + " places free");
                }

            #endregion

                #region If sendInterval has expired, send contents of the buffer to Azure
                if (_azureSendThreads < 8)
                {
                    lock (MainThreadLock)
                    {
                        _azureSendThreads++;
                    }

                    myAzureSendManager_Burner = new AzureSendManager_Burner(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable, e.MeasuredQuantity, "", caCerts, copyTimeOfLastSend, sendInterval_Burner, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                    myAzureSendManager_Burner.AzureCommandSend += myAzureSendManager_Burner_AzureCommandSend;
                    try { GHI.Processor.Watchdog.ResetCounter(); }
                    catch { };
                    //_Print_Debug("\r\nRow was sent on its way to Azure");
                    //Debug.Print("\r\nRow was sent on its way to Azure (Burner)");
                    myAzureSendManager_Burner.Start();

                    if (e.LastOfDay)   // Write the last message of the day to a separate table where the TableName is augmented with "Days" (eg. TestDays2018)                  
                    {
                        myAzureSendManager_Burner = new AzureSendManager_Burner(myCloudStorageAccount, timeZoneOffset, dstStart, dstEnd, dstOffset, e.DestinationTable + "Days", e.MeasuredQuantity, "", caCerts, copyTimeOfLastSend, sendInterval_Burner, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
                        myAzureSendManager_Burner.AzureCommandSend += myAzureSendManager_Burner_AzureCommandSend;
                        try { GHI.Processor.Watchdog.ResetCounter(); }
                        catch { };
                        //_Print_Debug("\r\nRow was sent on its way to Azure");
                        Debug.Print("\r\nLast Row of day was sent on its way to Azure");
                        myAzureSendManager_Burner.Start();
                    }

                }
                else
                {
                    _azureSendThreadResetCounter++;

                    if (_azureSendThreadResetCounter > 5)   // when _azureSendThread > 8 we write the next 5 rows coming through sensor events only to the buffer
                    // this should give outstanding requests time to finish
                    // then we reset the counters
                    {
                        _azureSendThreadResetCounter = 0;
                        _azureSendThreads = 0;
                    }

                }
                #endregion


                //Debug.Print("Burner Sensor sent actstate = " + e.ActState + " oldState = " + e.OldState + " at " + e.Timestamp);
            }
            else
            {
#if DebugPrint
                    Debug.Print("\r\nRow was discarded, sendInterval was not expired ");
#endif
            }

            #region Prepare rebooting of the mainboard e.g. if not enough ram due to memory leak
            if (_willRebootAfterNextEvent)
            {
#if DebugPrint
                    Debug.Print("Board is going to reboot in 3000 ms\r\n");
#endif
                Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
            }
            if (willReboot)
            { _willRebootAfterNextEvent = true; }
            #endregion


        }
        #endregion

        #region Rfm69 ack received (outcommented
        /*
        
        static void radio_ACKReturned(RFM69_NETMF sender, RFM69_NETMF.ACK_EventArgs e)
        {
            if (e.ACK_Received)
            {
                try
                {
                    // multicolorLED.BlinkOnce(GT.Color.Cyan);
#if DebugPrint
                        Debug.Print("ACK received from Node: " + e.senderOfTheACK + "  ~ms " + (DateTime.Now - e.sendTime).Milliseconds + " after asyncSendWithRetry");
#endif
                }
                catch { };
            }
            else
            {
                try
                {
#if DebugPrint
                        Debug.Print("No ACK was returned for message: " + new string(Encoding.UTF8.GetChars(e.sentData)));
#endif
                }
                catch { };
            }
        }
        
        */
        #endregion

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

        static void myButton_ButtonPressed(ButtonNETMF sender, ButtonNETMF.ButtonState state)
        {
            Debug.Print("Button was pressed");
        }
        #endregion

        #region myButton_ButtonReleased
        static void myButton_ButtonReleased(ButtonNETMF sender, ButtonNETMF.ButtonState state)
        {
            Debug.Print("Button was released");
        }
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
            switch (_AzureDebugMode)
            {
                //Do nothing             
                case AzureStorageHelper.DebugMode.NoDebug:
                    break;

                //Output Debugging info to the serial port
                case AzureStorageHelper.DebugMode.SerialDebug:

                    //Convert the message to bytes
                    /*
                    byte[] message_buffer = System.Text.Encoding.UTF8.GetBytes(message);
                    _debug_port.Write(message_buffer,0,message_buffer.Length);
                    */
                    break;

                //Print message to the standard debug output
                case AzureStorageHelper.DebugMode.StandardDebug:
#if DebugPrint
                        Debug.Print(message);
#endif
                    break;
            }
            //}
        }
        #endregion
    

        #region Event myAzureSendManager_Boiler_AzureCommandSend (Callback indicating e.g. that the entity was sent
        static void myAzureSendManager_Boiler_AzureCommandSend(AzureSendManager_Boiler sender, AzureSendManager_Boiler.AzureSendEventArgs e)
        {
            try { GHI.Processor.Watchdog.ResetCounter(); }
            catch { };

            lock (MainThreadLock)
            {
                if (e.decrementThreadCounter && (_azureSendThreads > 0))
                { _azureSendThreads--; }
            }
            if (e.azureCommandWasSent)
            {
                // _Print_Debug("Row was sent");
                // _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
                if ((e.returnCode == HttpStatusCode.Created) || (e.returnCode == HttpStatusCode.NoContent))
                {
#if SD_Card_Logging
                        var source_1 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "o.k.", logPosition = "HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Normal", source_1);
#endif

                    _azureSends++;
                }
                else
                {
#if SD_Card_Logging
                        var source_2 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "Bad HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Error", source_2);
#endif
                    _azureSendErrors++;
                }
            }
            else
            {
#if SD_Card_Logging
                    LogContent source_3 = null;
                    switch (e.Code)
                    {
                        case 7:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "No Connection", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 8:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "one try failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 2:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Object to early", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 1:
                            {
                                var source_4 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Buffer was empty", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                SdLoggerService.LogEventHourly("Normal", source_4);
                                break;
                            }
                        case 9:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "3 tries failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 5:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Failed to create Table", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        default:
                            { }
                            break;
                    }
                    if (source_3 != null)
                    {
                        SdLoggerService.LogEventHourly("Error", source_3);
                    }
#endif

                // _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
            }

            Debug.Print("AsyncCallback from OnOff send Thread (Boiler): " + e.Message);

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "End of method. Count of Threads = " + _azureSendThreads, logNumber = 2 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif
        }
        #endregion

        #region Event myAzureSendManager_Burner_AzureCommandSend (Callback indicating e.g. that the entity was sent
        static void myAzureSendManager_Burner_AzureCommandSend(AzureSendManager_Burner sender, AzureSendManager_Burner.AzureSendEventArgs e)
        {
            try { GHI.Processor.Watchdog.ResetCounter(); }
            catch { };

            lock (MainThreadLock)
            {
                if (e.decrementThreadCounter && (_azureSendThreads > 0))
                { _azureSendThreads--; }
            }
            if (e.azureCommandWasSent)
            {
               // _Print_Debug("Row was sent");
               // _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
                if ((e.returnCode == HttpStatusCode.Created) || (e.returnCode == HttpStatusCode.NoContent))
                {
#if SD_Card_Logging
                        var source_1 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "o.k.", logPosition = "HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Normal", source_1);
#endif

                    _azureSends++;
                }
                else
                {
#if SD_Card_Logging
                        var source_2 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "Bad HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Error", source_2);
#endif
                    _azureSendErrors++;
                }
            }
            else
            {
#if SD_Card_Logging
                    LogContent source_3 = null;
                    switch (e.Code)
                    {
                        case 7:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "No Connection", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 8:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "one try failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 2:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Object to early", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 1:
                            {
                                var source_4 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Buffer was empty", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                SdLoggerService.LogEventHourly("Normal", source_4);
                                break;
                            }
                        case 9:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "3 tries failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 5:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Failed to create Table", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        default:
                            { }
                            break;
                    }
                    if (source_3 != null)
                    {
                        SdLoggerService.LogEventHourly("Error", source_3);
                    }
#endif

               // _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
            }

            Debug.Print("AsyncCallback from OnOff send Thread (Burner): " + e.Message);

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "End of method. Count of Threads = " + _azureSendThreads, logNumber = 2 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif
        }
        #endregion

        #region Event myAzureSendManager_Solar_AzureCommandSend (Callback indicating e.g. that the entity was sent
        static void myAzureSendManager_Solar_AzureCommandSend(AzureSendManager_Solar sender, AzureSendManagerBase.AzureSendEventArgs e)
        {
                   try { GHI.Processor.Watchdog.ResetCounter(); }
            catch { };

            lock (MainThreadLock)
            {
                if (e.decrementThreadCounter && (_azureSendThreads > 0))
                { _azureSendThreads--; }
            }
            if (e.azureCommandWasSent)
            {
               // _Print_Debug("Row was sent");
               // _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
                if ((e.returnCode == HttpStatusCode.Created) || (e.returnCode == HttpStatusCode.NoContent))
                {
#if SD_Card_Logging
                        var source_1 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "o.k.", logPosition = "HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Normal", source_1);
#endif

                    _azureSends++;
                }
                else
                {
#if SD_Card_Logging
                        var source_2 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "Bad HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Error", source_2);
#endif
                    _azureSendErrors++;
                }
            }
            else
            {
#if SD_Card_Logging
                    LogContent source_3 = null;
                    switch (e.Code)
                    {
                        case 7:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "No Connection", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 8:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "one try failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 2:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Object to early", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 1:
                            {
                                var source_4 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Buffer was empty", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                SdLoggerService.LogEventHourly("Normal", source_4);
                                break;
                            }
                        case 9:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "3 tries failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 5:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Failed to create Table", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        default:
                            { }
                            break;
                    }
                    if (source_3 != null)
                    {
                        SdLoggerService.LogEventHourly("Error", source_3);
                    }
#endif

               // _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
            }

            Debug.Print("AsyncCallback from Solarpump On/Off send Thread (Solar): " + e.Message);

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "End of method. Count of Threads = " + _azureSendThreads, logNumber = 2 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif
        }
        
        #endregion

        #region Event myAzureSendManager_SolarTemps_AzureCommandSend (Callback indicating e.g. that the Current entity was sent


        static void myAzureSendManager_SolarTemps_AzureCommandSend(AzureSendManager_SolarTemps sender, AzureSendManager_SolarTemps.AzureSendEventArgs e)
        {
            try { GHI.Processor.Watchdog.ResetCounter(); }
            catch { };

            if (e.decrementThreadCounter && (_azureSendThreads > 0))
            { _azureSendThreads--; }

            if (e.azureCommandWasSent)
            {
                _Print_Debug("Row was sent");
                _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
                if ((e.returnCode == HttpStatusCode.Created) || (e.returnCode == HttpStatusCode.NoContent))
                {
#if SD_Card_Logging
                        var source_1 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "o.k.", logPosition = "HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Normal", source_1);
#endif
                    waitForCurrentCallback.Set();
                    _azureSends++;
                }
                else
                {
#if SD_Card_Logging
                        var source_2 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "Bad HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Error", source_2);
#endif
                    _azureSendErrors++;
                }
            }
            else
            {
#if SD_Card_Logging
                    LogContent source_3 = null;
                    switch (e.Code)
                    {
                        case 7:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "No Connection", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 8:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "one try failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 2:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Object to early", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 1:
                            {
                                var source_4 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Buffer was empty", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                SdLoggerService.LogEventHourly("Normal", source_4);
                                break;
                            }
                        case 9:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "3 tries failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 5:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Failed to create Table", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        default:
                            { }
                            break;
                    }
                    if (source_3 != null)
                    {
                        SdLoggerService.LogEventHourly("Error", source_3);
                    }
#endif

                _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
            }

            Debug.Print("AsyncCallback from Rfm69 SolarTemps Data send Thread: " + e.Message);

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "End of method. Count of Threads = " + _azureSendThreads, logNumber = 2 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif
        }
        #endregion

        #region Event myAzureSendManager_AzureCommandSend (Callback indicating e.g. that the Current entity was sent
        static void myAzureSendManager_AzureCommandSend(AzureSendManager sender, AzureSendManager.AzureSendEventArgs e)
        {
            try { GHI.Processor.Watchdog.ResetCounter(); }
            catch { };

            if (e.decrementThreadCounter && (_azureSendThreads > 0))
            { _azureSendThreads--; }

            if (e.azureCommandWasSent)
            {
                _Print_Debug("Row was sent");
                _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
                if ((e.returnCode == HttpStatusCode.Created) || (e.returnCode == HttpStatusCode.NoContent))
                {
#if SD_Card_Logging
                        var source_1 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "o.k.", logPosition = "HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Normal", source_1);
#endif
                    waitForCurrentCallback.Set();
                    _azureSends++;
                }
                else
                {
#if SD_Card_Logging
                        var source_2 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "Bad HttpStatusCode: " + e.returnCode.ToString(), logNumber = e.Code };
                        SdLoggerService.LogEventHourly("Error", source_2);
#endif
                    _azureSendErrors++;
                }
            }
            else
            {
#if SD_Card_Logging
                    LogContent source_3 = null;
                    switch (e.Code)
                    {
                        case 7:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "No Connection", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 8:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "one try failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 2:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Object to early", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 1:
                            {
                                var source_4 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Buffer was empty", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                SdLoggerService.LogEventHourly("Normal", source_4);
                                break;
                            }
                        case 9:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "3 tries failed", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        case 5:
                            {
                                source_3 = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "Failed to create Table", logPosition = "HttpStatusCode ambiguous: " + e.returnCode.ToString(), logNumber = e.Code };
                                break;
                            }
                        default:
                            { }
                            break;
                    }
                    if (source_3 != null)
                    {
                        SdLoggerService.LogEventHourly("Error", source_3);
                    }
#endif

                _Print_Debug("Count of AzureSendThreads = " + _azureSendThreads);
            }

           Debug.Print("AsyncCallback from Rfm69 Current Data send Thread: " + e.Message);

#if SD_Card_Logging
                var source = new LogContent() { logOrigin = "Event: Azure command sent", logReason = "n", logPosition = "End of method. Count of Threads = " + _azureSendThreads, logNumber = 2 };
                SdLoggerService.LogEventHourly("Normal", source);
#endif
        }
        #endregion

    }
}


