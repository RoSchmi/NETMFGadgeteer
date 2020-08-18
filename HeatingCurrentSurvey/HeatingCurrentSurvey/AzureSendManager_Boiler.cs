using System;
using Microsoft.SPOT;
using System.Collections;
using System.Threading;
using System.Net;
using RoSchmi.Net.Azure.Storage;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using RoSchmi.DayLihtSavingTime;


namespace HeatingSurvey
{
    class AzureSendManager_Boiler : AzureSendManagerBase
    {
        #region fields belonging to AzureSendManager_2
        //****************  SendManager *************************************
        static int yearOfLastSend = 2000;
        static string prefixOfLastTable = "";
       
        public static int _forcedReboots = 0;
        public static int _badReboots = 0;
        public static  int _azureSends = 0;
        public static  int _azureSendErrors = 0;

        public static  string _tablePrefix = "Y";
        public static  string _sensorValueHeader = "";
        public static  string _socketSensorHeader = "";
        public static  TimeSpan _sendInterval;

        public static  string _lastResetCause = "";
        public static  DateTime _timeOfLastSend;
        public static  DateTime _timeOfLastSensorEvent;
        public static  DateTime sampleTimeOfLastSent;
        public static  int _iteration = 0;
        public static  TimeSpan _onTimeDay = new TimeSpan(0);
        public static  TimeSpan _onTimeWeek = new TimeSpan(0);
        public static  TimeSpan _onTimeMonth = new TimeSpan(0);
        public static  TimeSpan _onTimeYear = new TimeSpan(0);
        public static  int _CD = 0;
        public static  int _CW = 0;
        public static  int _CM = 0;
        public static  int _CY = 0;
            
        TableClient table;
                                  
        //*******************************************************************************
        #endregion

        #region fields belonging to Queue Buffer
        // This is the Queue to hold the measured values
        //private const int _defaultCapacity = 256;
        private const int _defaultCapacity = 20;

        private const int _defaultPreFillLevel = 1;
        private static bool _preFillLevelReached = true;
       
        private static OnOffSample[] _buffer = new OnOffSample[_defaultCapacity];
        
        private static int _head;
        private static int _tail;
        private static int _count;
        private static int _capacity;
        private static int _preFillLevel;
        #endregion

        #region Methods belonging to Queue Buffer
        public static void InitializeQueue()
        {
            _capacity = _defaultCapacity;
            _head = 0;
            _tail = 0;
            _count = 0;
            _preFillLevel = _defaultPreFillLevel;
            _preFillLevelReached = true;
        }

        public static int Count
        {
            get { return _count; }
        }

        public int preFillLevel
        {
            get { return _preFillLevel; }
            set { _preFillLevel = value; }
        }

        public static int capacity
        {
            get { return _capacity; }
        }

        public bool preFillLevelReached
        {
            get { return _preFillLevelReached; }
            set { _preFillLevelReached = value; }
        }

        public static void Clear()
        {
            _count = 0;
            _tail = _head;
        }


        public static bool hasFreePlaces(int value = 1)
        {
            lock (theLock)
            {
                if (_count + value <= _capacity)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
       
        public static void EnqueueSampleValue(OnOffSample SampleValue)
        {
            lock (theLock)
            {
                if (_count == _capacity)
                {
                    Grow();
                    //Debug.Print("New Capacity: " + _buffer.Length);
                }
                _buffer[_head] = SampleValue;
                _head = (_head + 1) % _capacity;
                _count++;
            }
        }


        public static OnOffSample PreViewNextSampleValue()
        {
            lock (theLock)
            {
                OnOffSample Value = DequeueSampleValue(true);
                return Value;
            }
        }

        public static OnOffSample DequeueNextSampleValue()
        {
            lock (theLock)
            {
                OnOffSample Value = DequeueSampleValue(false);
                return Value;
            }
        }

        private static OnOffSample DequeueSampleValue(bool PreView)
        {
            lock (theLock)
            {
                if (_count > 0)
                {
                    OnOffSample value = _buffer[_tail];

                    if (!PreView)
                    {
                        _tail = (_tail + 1) % _capacity;
                        _count--;
                    }
                    return value;
                }
                else
                {
                    return null;
                }
            }
        }

        private static void Grow()
        {
            int newCapacity = _capacity << 1;
            OnOffSample[] newBuffer = new OnOffSample[newCapacity];

            if (_tail < _head)
            {
                Array.Copy(_buffer, _tail, newBuffer, 0, _count);
            }
            else
            {
                Array.Copy(_buffer, _tail, newBuffer, 0, _capacity - _tail);
                Array.Copy(_buffer, 0, newBuffer, _capacity - _tail, _head);
            }
            _buffer = newBuffer;
            _head = _count;
            _tail = 0;
            _capacity = newCapacity;
        }
        #endregion


        #region AzureSendManager Constructor
        public AzureSendManager_Boiler(CloudStorageAccount pCloudStorageAccount, int pTimeZoneOffset, string pDstStart, string pDstEnd, int pDstOffset, string pTablePreFix, string pSensorValueHeader, string pSocketSensorHeader, X509Certificate[] pCaCerts, DateTime pTimeOfLastSend, TimeSpan pSendInterval, int pAzureSends, AzureStorageHelper.DebugMode pDebugMode, AzureStorageHelper.DebugLevel pDebugLevel, IPAddress pFiddlerIPAddress, bool pAttachFiddler, int pFiddlerPort, bool pUseHttps)
            : base(pCloudStorageAccount, pTimeZoneOffset, pDstStart, pDstEnd, pDstOffset, pCaCerts, pDebugMode, pDebugLevel, pFiddlerIPAddress, pAttachFiddler, pFiddlerPort, pUseHttps)
        {           
            _azureSends = pAzureSends;
            _tablePrefix = pTablePreFix;
            _sensorValueHeader = pSensorValueHeader;
            _socketSensorHeader = pSocketSensorHeader;           
            _timeOfLastSend = pTimeOfLastSend;
            _sendInterval = pSendInterval;
                                 
        }
        #endregion

        #region public method Start sending contents of buffer
        public void Start()
        {
            Thread sendThread = new Thread(new ThreadStart(runSendThread));
            sendThread.Start();
        }
        #endregion
      
        #region runSendThread
        public void runSendThread()
        {
                OnOffSample nextSampleValue;
                HttpStatusCode createTableReturnCode;
                HttpStatusCode insertEntityReturnCode = HttpStatusCode.Ambiguous;
                int actYear = DateTime.Now.Year;
                string tableName = _tablePrefix + actYear.ToString();

                bool nextSampleValueIsNull = false;
                bool nextSampleValueShallBeSent = false;

                int loopCtr = 0;
                while (loopCtr < 3)   // We try 3 times to deliver to Azure, if not accepted, we neglect
                {
                        nextSampleValue = PreViewNextSampleValue();
                        nextSampleValueIsNull = (nextSampleValue == null) ? true : false;
                        nextSampleValueShallBeSent = false;
                        if (!nextSampleValueIsNull)
                        {
                            nextSampleValueShallBeSent = (nextSampleValue.ForceSend || ((nextSampleValue.TimeOfSample - sampleTimeOfLastSent) > _sendInterval)) ? true : false;
                        }
                    if (nextSampleValueIsNull)
                    {
                        this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 1, tableName + _Buffer_empty));
                        //Debug.Print("Leaving because buffer is empty. Count in buffer when leaving AzureSendManager = " + Count);
                        break;
                    }
                    if (!nextSampleValueShallBeSent)
                    {
                        nextSampleValue = DequeueNextSampleValue();    // Discard this to early object
                        this.OnAzureCommandSend(this, new AzureSendEventArgs(false, false, HttpStatusCode.Ambiguous, 2, tableName + _Early_object_discarded));
                    }

                    #region Create an Azure Table, Name = _tablePrefix plus the actual year (only when needed)
                    if ((DateTime.Now.Year != yearOfLastSend)|| (_tablePrefix != prefixOfLastTable))
                    {
                        actYear = DateTime.Now.Year;
                        tableName = _tablePrefix + actYear.ToString();
                        lock (theLock)
                        {
                            createTableReturnCode = createTable(_CloudStorageAccount, tableName);
                        }

                        //createTableReturnCode = HttpStatusCode.NotAcceptable;
                        //createTableReturnCode = HttpStatusCode.Ambiguous;
                        if (createTableReturnCode == HttpStatusCode.Created)
                        {
                            //Debug.Print("Table was created: " + tableName + ". HttpStatusCode: " + createTableReturnCode.ToString());
                            this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 3, tableName + _Table_created));
                            yearOfLastSend = actYear;
                            prefixOfLastTable = _tablePrefix;
                        }
                        else
                        {
                            if (createTableReturnCode == HttpStatusCode.Conflict)
                            {
                                //Debug.Print("Table " + tableName + " already exists");
                                yearOfLastSend = actYear;
                                prefixOfLastTable = _tablePrefix;
                                this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 4, tableName + _Table_already_exists ));
                            }
                            else
                            {
                                if (createTableReturnCode == HttpStatusCode.NoContent)
                                {
                                    //Debug.Print("Create Table operation. HttpStatusCode: " + createTableReturnCode.ToString());
                                    yearOfLastSend = actYear;
                                    prefixOfLastTable = _tablePrefix;
                                }
                                else
                                {
                                    //Debug.Print("Failed to create Table " + tableName + ". HttpStatusCode: " + createTableReturnCode.ToString());
                                    this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 5, tableName + _Failed_to_create_Table));
                                    Thread.Sleep(10000);
                                    Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
                                    while (true)
                                    {
                                        Thread.Sleep(100);
                                    }
                                    //break;
                                }
                            }
                        }
                        Thread.Sleep(3000);
                    }
                    #endregion

                    #region Create an ArrayList  to hold the properties of the entity
                    this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 6, tableName + _Going_to_insert_Entity));
                    //Debug.Print("\r\nGoing to insert an entity");
                    // Now we create an Arraylist to hold the properties of a table Row,
                    // write these items to an entity
                    // and send this entity to the Cloud

                    ArrayList propertiesAL = new System.Collections.ArrayList();
                    lock (theLock)
                    {
                        propertiesAL = createOnOffPropertyArrayList(nextSampleValue, _azureSends);
                    }

                   
                    #endregion

                    //Thread.Sleep(1100);
                    //DateTime actDate = DateTime.Now;
                    DateTime actDate = nextSampleValue.TimeOfSample;

                    //calculate reverse Date, so the last entity can be retrieved with the Azure $top1 query
                    string reverseDate = (10000 - actDate.Year).ToString("D4") + (12 - actDate.Month).ToString("D2") + (31 - actDate.Day).ToString("D2")
                           + (23 - actDate.Hour).ToString("D2") + (59 - actDate.Minute).ToString("D2") + (59 - actDate.Second).ToString("D2");

                    TempEntity myTempEntity;
                    lock(theLock)
                    {
                        myTempEntity = new TempEntity(nextSampleValue.PartitionKey, reverseDate, propertiesAL);
                    }
                    string insertEtag = null;

                    insertEntityReturnCode = insertTableEntity(_CloudStorageAccount, tableName, myTempEntity, out insertEtag);

                    #region Outcommented (for tests)
                           // only for testing to produce entity that is rejected by Azure
                           //insertEntityReturnCode = insertTableEntity(_CloudStorageAccount, tableName.Substring(0, 6), myTempEntity, out insertEtag);

                           //****************  to delete ****************************
                           /*
                                if (DateTime.Now < new DateTime(2016, 8, 2, 0, 31, 1))
                                {
                                    Debug.Print("Löschen geblockt");
                                    insertEntityReturnCode = HttpStatusCode.Ambiguous;
                                }
                                else
                                {
                                    Debug.Print("Löschen erlaubt");
                                }
                           */
                           //*********************************************************
                    #endregion

                    if ((insertEntityReturnCode == HttpStatusCode.Created) || (insertEntityReturnCode == HttpStatusCode.NoContent))
                    {
                        //Debug.Print("Entity was inserted. Try: " + loopCtr.ToString() + " HttpStatusCode: " + insertEntityReturnCode.ToString());
                        
                        nextSampleValue = DequeueNextSampleValue();
                        if (nextSampleValue != null)
                        {
                            if (!nextSampleValue.ForceSend)
                            { sampleTimeOfLastSent = nextSampleValue.TimeOfSample; }
                        }
                            
                       
                        this.OnAzureCommandSend(this, new AzureSendEventArgs(true, false, insertEntityReturnCode, 0, tableName + _Entity_was_inserted));

                         // don't break, the loop is left when we try to get the next row until it is null
                    }
                    else
                    {
                        if (insertEntityReturnCode == HttpStatusCode.Ambiguous) // this is returned when no contact to internet
                        {
                            yearOfLastSend = 2000;
                            this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 7, "No Internet access"));
                            //Debug.Print("Leaving because of no internet access. Count in buffer when leaving AzureSendManager = " + Count);
                            break;
                        }
                        else
                        {
                            yearOfLastSend = 2000;
                            this.OnAzureCommandSend(this, new AzureSendEventArgs(false, false, HttpStatusCode.Ambiguous, 8, "Failed to insert Entity, one try"));
                            //Debug.Print("Failed to insert Entity, Try: " + loopCtr.ToString() + " HttpStatusCode: " + insertEntityReturnCode.ToString());
                            Thread.Sleep(3000);
                            loopCtr++;
                        }
                    }

                    if (loopCtr >= 3)           // if Azure does not accept the entity, we give up after the third try and discard this entity
                    {
                        nextSampleValue = DequeueNextSampleValue();
                        this.OnAzureCommandSend(this, new AzureSendEventArgs(false, true, HttpStatusCode.Ambiguous, 9, "Failed to insert Entity after 3 tries"));
                            //Debug.Print("Leaving because Entity was discarded after 3rd try. Count in buffer when leaving AzureSendManager = " + Count);
                        if (DateTime.Now < new DateTime(2016, 7, 1))       // Reboot if Entity can not be inserted, probably because of wrong time
                        {
                            Thread.Sleep(20000);
                            Microsoft.SPOT.Hardware.PowerState.RebootDevice(true, 3000);
                            while (true)
                            {
                                Thread.Sleep(100);
                            }
                        }
                        break;
                        }
                    Thread.Sleep(1);
                }
                //Debug.Print("Jumped out of while loop");

                //Debug.Print("Count in buffer when entering AzureSendManager = " + Count);


                
        }
        #endregion

        
        #region Method ActualizeFromLastAzureRow
        public Counters ActualizeFromLastAzureRow(ref string pSwitchMessage)
        {
#if DebugPrint
                    Debug.Print("\r\nGoing to query for Entities");
#endif
            // Now we query for the last row of the table as selected by the query string "$top=1"
            // (OLS means Of the Last Send)
            string readTimeOLS = DateTime.Now.ToString();  // shall hold send time of the last entity on Azure
            const string noTime = "000-00:00:00";
            string onTimeDay = noTime;
            string onTimeWeek = noTime;
            string onTimeMonth = noTime;
            string onTimeYear = noTime;           
            string cD = "0";
            string cW = "0";
            string cM = "0";
            string cY = "0";


            ArrayList queryArrayList = new ArrayList();
            //myAzureSendManager_Boiler = new AzureSendManager_Boiler(myCloudStorageAccount, timeZoneOffset, _tablePreFix, _sensorValueHeader, _socketSensorHeader, caCerts, _timeOfLastSend, sendInterval, _azureSends, _AzureDebugMode, _AzureDebugLevel, IPAddress.Parse(fiddlerIPAddress), pAttachFiddler: attachFiddler, pFiddlerPort: fiddlerPort, pUseHttps: Azure_useHTTPS);
            try { GHI.Processor.Watchdog.ResetCounter(); }
            catch { };
            HttpStatusCode queryEntityReturnCode = queryTableEntities("$top=1", out queryArrayList);

            if (queryEntityReturnCode == HttpStatusCode.OK)
            {
#if DebugPrint
                        Debug.Print("Query for entities completed. HttpStatusCode: " + queryEntityReturnCode.ToString());
#endif
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };
                if (queryArrayList.Count != 0)
                {
                    var entityHashtable = queryArrayList[0] as Hashtable;
                    string lastBootReason = entityHashtable["bR"].ToString();
                    if (lastBootReason == "X")     // reboot was forced by the program (not enougth free ram)
                    {
                        _lastResetCause = "ForcedReboot";
                        try
                        {
                            _forcedReboots = int.Parse(entityHashtable["forcedReboots"].ToString()) + 1;
                            _badReboots = int.Parse(entityHashtable["badReboots"].ToString());
                            _azureSends = int.Parse(entityHashtable["Sends"].ToString()) + 1;
                            _azureSendErrors = int.Parse(entityHashtable["sendErrors"].ToString());
                        }
                        catch { }
                    }
                    else
                    {
                        try
                        {
                            _forcedReboots = int.Parse(entityHashtable["forcedReboots"].ToString());
                            _badReboots = int.Parse(entityHashtable["badReboots"].ToString()) + 1;
                            _azureSends = int.Parse(entityHashtable["Sends"].ToString()) + 1;
                            _azureSendErrors = int.Parse(entityHashtable["sendErrors"].ToString());
                        }
                        catch { }
                    }

                    try
                    {
                        onTimeDay = entityHashtable["OnTimeDay"].ToString();
                        onTimeWeek = entityHashtable["OnTimeWeek"].ToString();
                        onTimeMonth = entityHashtable["OnTimeMonth"].ToString();
                        onTimeYear = entityHashtable["OnTimeYear"].ToString();
                        cD = entityHashtable["CD"].ToString();
                        cW = entityHashtable["CW"].ToString();
                        cM = entityHashtable["CM"].ToString();
                        cY = entityHashtable["CY"].ToString();
                    }
                    catch { }

                    _onTimeDay = new TimeSpan(int.Parse(onTimeDay.Substring(0, 3)), int.Parse(onTimeDay.Substring(4, 2)), int.Parse(onTimeDay.Substring(7, 2)), int.Parse(onTimeDay.Substring(10, 2)));

                    readTimeOLS = entityHashtable["SampleTime"].ToString();

                    try
                    {
                        _timeOfLastSend = new DateTime(int.Parse(readTimeOLS.Substring(6, 4)), int.Parse(readTimeOLS.Substring(0, 2)),
                                                       int.Parse(readTimeOLS.Substring(3, 2)), int.Parse(readTimeOLS.Substring(11, 2)),
                                                       int.Parse(readTimeOLS.Substring(14, 2)), int.Parse(readTimeOLS.Substring(17, 2)));

                        // calculate back to the time without dayLightSavingTime offset
                        _timeOfLastSend = _timeOfLastSend.AddMinutes(-DayLihtSavingTime.DayLightTimeOffset(dstStart, dstEnd, dstOffset, _timeOfLastSend, true));
                    }
                    catch
                    {
                        _timeOfLastSend = DateTime.Now.AddHours(-1.0);  // if something goes wrong, take DateTime.Now minus 1 hour;
                    }

                    try
                    {
                        if (_timeOfLastSend.Day == _timeOfLastSensorEvent.Day)
                        {
                            _onTimeDay = new TimeSpan(int.Parse(onTimeDay.Substring(0, 3)), int.Parse(onTimeDay.Substring(4, 2)), int.Parse(onTimeDay.Substring(7, 2)), int.Parse(onTimeDay.Substring(10, 2)));
                            _onTimeWeek = new TimeSpan(int.Parse(onTimeWeek.Substring(0, 3)), int.Parse(onTimeWeek.Substring(4, 2)), int.Parse(onTimeWeek.Substring(7, 2)), int.Parse(onTimeWeek.Substring(10, 2)));
                            _onTimeMonth = new TimeSpan(int.Parse(onTimeMonth.Substring(0, 3)), int.Parse(onTimeMonth.Substring(4, 2)), int.Parse(onTimeMonth.Substring(7, 2)), int.Parse(onTimeMonth.Substring(10, 2)));
                            _onTimeYear = new TimeSpan(int.Parse(onTimeYear.Substring(0, 3)), int.Parse(onTimeYear.Substring(4, 2)), int.Parse(onTimeYear.Substring(7, 2)), int.Parse(onTimeYear.Substring(10, 2)));
                            _CD = int.Parse(cD);
                            _CW = int.Parse(cW);
                            _CM = int.Parse(cM);
                            _CY = int.Parse(cY);
                        }
                        else
                        {
                            _onTimeDay = new TimeSpan(0);
                            _CD = 0;
                            if (!((_timeOfLastSend.DayOfWeek == DayOfWeek.Sunday) && (_timeOfLastSensorEvent.DayOfWeek == DayOfWeek.Monday)))
                            {
                                _onTimeWeek = new TimeSpan(int.Parse(onTimeWeek.Substring(0, 3)), int.Parse(onTimeWeek.Substring(4, 2)), int.Parse(onTimeWeek.Substring(7, 2)), int.Parse(onTimeWeek.Substring(10, 2)));
                                _CW = int.Parse(cW);
                            }
                            else
                            {
                                _onTimeWeek = new TimeSpan(0);
                                _CW = 0;
                            }
                            if (_timeOfLastSend.Month == _timeOfLastSensorEvent.Month)
                            {
                                _onTimeMonth = new TimeSpan(int.Parse(onTimeMonth.Substring(0, 3)), int.Parse(onTimeMonth.Substring(4, 2)), int.Parse(onTimeMonth.Substring(7, 2)), int.Parse(onTimeMonth.Substring(10, 2)));
                                _CM = int.Parse(cM);
                            }
                            else
                            {
                                _onTimeMonth = new TimeSpan(0);
                                _CM = 0;
                            }
                            if (_timeOfLastSend.Year == _timeOfLastSensorEvent.Year)
                            {
                                _onTimeYear = new TimeSpan(int.Parse(onTimeYear.Substring(0, 3)), int.Parse(onTimeYear.Substring(4, 2)), int.Parse(onTimeYear.Substring(7, 2)), int.Parse(onTimeYear.Substring(10, 2)));
                                _CY = int.Parse(cY);
                            }
                            else
                            {
                                _onTimeYear = new TimeSpan(0);
                                _CY = 0;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _onTimeDay = new TimeSpan(0);
                        _onTimeWeek = new TimeSpan(0);
                        _onTimeMonth = new TimeSpan(0);
                        _onTimeYear = new TimeSpan(0);
                        _CD = 0;
                        _CW = 0;
                        _CM = 0;
                        _CY = 0;
                    }
                }
                else
                {
                    _timeOfLastSend = DateTime.Now;
                }
            }
            else
            {
                try { GHI.Processor.Watchdog.ResetCounter(); }
                catch { };
                _timeOfLastSend = DateTime.Now;


#if DebugPrint
                        Debug.Print("Failed to query Entities. HttpStatusCode: " + queryEntityReturnCode.ToString());
#endif

            }

            pSwitchMessage = "Reboot: " + _lastResetCause;

            Counters returnValue = new Counters();
            returnValue.AzureSends = _azureSends;
            returnValue.AzureSendErrors = _azureSendErrors;
            returnValue.ForcedReboots = _forcedReboots;
            returnValue.BadReboots = _badReboots;

            return returnValue;
        }
        #endregion

        #region public Method queryTableEntities
        public static HttpStatusCode queryTableEntities(string query, out ArrayList queryResult)
        {
            // Now we query for the last row of the table as selected by the query string "$top=1"
            ArrayList queryArrayList = new ArrayList();

            //This operation does not work with https, so the CloudStorageAccount is set to use http
            _CloudStorageAccount = new CloudStorageAccount(_CloudStorageAccount.AccountName, _CloudStorageAccount.AccountKey, useHttps: false);

            string tableName = _tablePrefix + DateTime.Now.Year;
            HttpStatusCode queryEntityReturnCode = queryTableEntities(_CloudStorageAccount, tableName, "$top=1", out queryArrayList);


            _CloudStorageAccount = new CloudStorageAccount(_CloudStorageAccount.AccountName, _CloudStorageAccount.AccountKey, useHttps: _useHttps);  // Reset Cloudstorageaccount to the original settings (http or https)
            /*
            if (queryEntityReturnCode == HttpStatusCode.OK)
            { Debug.Print("Query for entities completed. HttpStatusCode: " + queryEntityReturnCode.ToString()); }
            else
            { Debug.Print("Failed to query Entities. HttpStatusCode: " + queryEntityReturnCode.ToString()); }
            */
            queryResult = queryArrayList;
            return queryEntityReturnCode;
        }
        #endregion

        #region private method queryTableEntities
        internal static HttpStatusCode queryTableEntities(CloudStorageAccount pCloudStorageAccount, string tableName, string query, out ArrayList queryResult)
        {
            //TableClient table = new TableClient(pCloudStorageAccount, caCerts, _timeZoneOffset, _debug, _debug_level);
            TableClient table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level);


            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, fiddlerIPAddress, fiddlerPort); }

            HttpStatusCode resultCode = table.QueryTableEntities(tableName, query, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIatomIxml, useSharedKeyLite: false);
            // now we can get the results by reading the properties: table.OperationResponse......
            queryResult = table.OperationResponseQueryList;
            // var body = table.OperationResponseBody;
            // this shows how to get a special value (here the RowKey)of the first entity
            // var entityHashtable = queryResult[0] as Hashtable;
            // var theRowKey = entityHashtable["RowKey"];
            return resultCode;
        }
        #endregion

        #region private method insertTableEntity
        internal HttpStatusCode insertTableEntity(CloudStorageAccount pCloudStorageAccount, string pTable, TableEntity pTableEntity, out string pInsertETag)
        {
            //TableClient table = new TableClient(pCloudStorageAccount, caCerts, _timeZoneOffset, _debug, _debug_level);
            TableClient table = new TableClient(pCloudStorageAccount, caCerts, _debug, _debug_level);
            // To use Fiddler as WebProxy include the following line. Use the local IP-Address of the PC where Fiddler is running
            // see: -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
            if (attachFiddler)
            { table.attachFiddler(true, fiddlerIPAddress, fiddlerPort); }

            var resultCode = table.InsertTableEntity(pTable, pTableEntity, TableClient.ContType.applicationIatomIxml, TableClient.AcceptType.applicationIjson, TableClient.ResponseType.dont_returnContent, useSharedKeyLite: false);
            pInsertETag = table.OperationResponseETag;
            //var body = table.OperationResponseBody;
            //Debug.Print("Entity inserted");
            return resultCode;
        }
        #endregion

        #region Delegate
        /// <summary>
        /// The delegate that is used to handle the IR event.
        /// </summary>
        /// <param name="sender">The <see cref="R_433_Receiver"/> object that raised the event.</param>
        /// <param name="e">The event arguments.</param>
        //public delegate void SignalReceivedEventHandler(RF_433_Receiver sender, SignalReceivedEventArgs e);
        public delegate void AzureSendManagerEventHandler(AzureSendManager_Boiler sender, AzureSendEventArgs e);

        /// <summary>
        /// Raised when the module detects an 433 Mhz signal.
        /// </summary>
        public event AzureSendManagerEventHandler AzureCommandSend;

        private AzureSendManagerEventHandler onAzureCommandSend;

        private void OnAzureCommandSend(AzureSendManager_Boiler sender, AzureSendEventArgs e)
        {
            if (this.onAzureCommandSend == null)
            { 
                this.onAzureCommandSend = this.OnAzureCommandSend;
            }
            //Changed by RoSchmi
            //if (Program.CheckAndInvoke(this.AzureCommandSend, this.onAzureCommandSend, sender, e))
            this.AzureCommandSend(sender, e);
        }
        #endregion
    }
}