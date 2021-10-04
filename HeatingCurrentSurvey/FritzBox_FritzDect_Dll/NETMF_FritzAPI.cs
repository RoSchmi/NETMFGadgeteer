using System;
using Microsoft.SPOT;
using RoSchmi.Net;
using Fritzbox.RoSchmi.Net;
using System.Collections;
using System.IO;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using Fritzbox.PervasiveDigital.Utilities;
using System.Threading;
using System.Security.Cryptography.X509Certificates;



namespace RoSchmi.Net.Fritzbox
{

    public class NETMF_FritzAPI
    {
        string user;
        string password;
        string fritzUrl;
        string sid = string.Empty;
        Uri uri;
        int port = 443;
        bool useHttps;


        private bool _fiddlerIsAttached = false;
        private IPAddress _fiddlerIP = null;
        private int _fiddlerPort = 8888;

        #region "Debugging"
        private FritzHttpHelper.DebugMode _debug = FritzHttpHelper.DebugMode.NoDebug;
        private FritzHttpHelper.DebugLevel _debug_level = FritzHttpHelper.DebugLevel.DebugErrors;
        //private FritzHttpHelper.DebugMode _debug = FritzHttpHelper.DebugMode.StandardDebug;
        //private FritzHttpHelper.DebugLevel _debug_level = FritzHttpHelper.DebugLevel.DebugAll;

        #endregion

        private const string login_sidService = "/login_sid.lua?";

        private const string homeautoswitchService = "/webservices/homeautoswitch.lua?";

        #region Region Constructor
        // Constructor
        public NETMF_FritzAPI(string fritzUser, string fritzPassword, string pFritzUrl, bool pUseHttps)
        {
            user = fritzUser;
            password = fritzPassword;
            fritzUrl = pFritzUrl;
            useHttps = pUseHttps;
            port = pUseHttps ? 443 : 80;
            uri = new Uri(StringUtilities.Format("{0}://{1}", pUseHttps ? "https" : "http", pFritzUrl));
        }
        #endregion

        #region Region init
        public bool init()
        {
            FritzHttpHelper.SetDebugMode(_debug);
            FritzHttpHelper.SetDebugMode(_debug);
            FritzHttpHelper.SetDebugLevel(_debug_level);

            // Gets challenge and MD5 encoded Password hash
            // response is <Challenge>-<MD5-Hash>
            String response = getChallengeResponse();   // returns "" in case of exception
            if (response == "")
            {
                return false;
            }
            sid = getSID(response);
            Debug.Print("SID: " + sid);
            if (sid == "")
            {
                //Debug.Print("FRITZ_ERR_EMPTY_SID");
                return false;
            }
            else if (sid == "0000000000000000")
            {
                //Debug.Print("FRITZ_ERR_INVALID_SID");
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion


        #region Region getSwitchPower
        public string getSwitchPower(String ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=getswitchpower&sid=", sid);
            string homeautoswitchService = "/webservices/homeautoswitch.lua?";
            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion

        #region Region getSwitchEnergy
        public string getSwitchEnergy(String ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=getswitchenergy&sid=", sid);
            string homeautoswitchService = "/webservices/homeautoswitch.lua?";
            return executeRequest(homeautoswitchService, cmdSuffix);

        }
        #endregion

        #region Region getTemperature
        public string getTemperature(String ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=gettemperature&sid=", sid);
            string homeautoswitchService = "/webservices/homeautoswitch.lua?";
            return executeRequest(homeautoswitchService, cmdSuffix);

        }
        #endregion

        #region Region testSID
        public string testSID()
        {
            string cmdSuffix = StringUtilities.Format("sid={0}", sid);
            string result = executeRequest(login_sidService, cmdSuffix);

            return result.Substring(result.IndexOf("<SID>") + 5, result.IndexOf("</SID>") - (result.IndexOf("<SID>") + 5));
        }
        #endregion

        #region Region getSwitchName
        public string getSwitchName(string ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=getswitchname&sid=", sid);
            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion

        #region Region getSwitchPresent
        public string getSwitchPresent(string ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=getswitchpresent&sid=", sid);

            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion

        #region Region setSwitchOn
        public string setSwitchOn(string ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=setswitchon&sid=", sid);
            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion

        #region Region setSwitchOff
        public string setSwitchOff(string ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=setswitchoff&sid=", sid);
            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion

        #region Region setSwitchToggle
        public string setSwitchToggle(string ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=setswitchtoggle&sid=", sid);
            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion

        #region Region getSwitchState
        public string getSwitchState(string ain)
        {
            string cmdSuffix = StringUtilities.Format("{0}{1}{2}{3}", "ain=", ain, "&switchcmd=getswitchstate&sid=", sid);
            return executeRequest(homeautoswitchService, cmdSuffix);
        }
        #endregion


        #region Region private getChallengeResponse
        private string getChallengeResponse()
        {
            uri = new Uri(StringUtilities.Format("{0}://{1}{2}", useHttps ? "https" : "http", fritzUrl, "/login_sid.lua"));
            BodyHttpResponse response = new BodyHttpResponse();
            try
            {
                response = FritzHttpHelper.SendWebRequest(uri, null, 0, "GET", false, null);

                string szblockTime = response.Body.Substring(response.Body.IndexOf("<BlockTime>") + 11, response.Body.IndexOf("</BlockTime>") - (response.Body.IndexOf("<BlockTime>") + 11));

                int blockTime = int.Parse(szblockTime);
                if (blockTime > 0)
                {
                    DateTime startTime = DateTime.Now;
                    while (DateTime.Now < startTime.AddSeconds(blockTime))
                    {
                        Thread.Sleep(20);
                    }
                }

                string challenge = response.Body.Substring(response.Body.IndexOf("<Challenge>") + 11, response.Body.IndexOf("</Challenge>") - (response.Body.IndexOf("<Challenge>") + 11));
                string challengeResponse = challenge + "-" + password;

                byte[] challengeRespByteArray = UTF8Encoding.UTF8.GetBytes(challengeResponse);
                byte[] Bit16ByteArray = new byte[challengeRespByteArray.Length * 2];

                //Convert to 16 bit
                int i = 0;
                int x = 0;
                while (x < challengeRespByteArray.Length)
                {
                    Bit16ByteArray[i] = challengeRespByteArray[x];
                    i++;
                    Bit16ByteArray[i] = 0x00;
                    i++;
                    x++;
                }
                byte[] hash;
                using (HashAlgorithm csp = new HashAlgorithm(HashAlgorithmType.MD5))
                {
                    hash = csp.ComputeHash(Bit16ByteArray);
                }

                return challenge + "-" + ByteExtensions.ToHexString(hash, "").ToLower();

            }
            catch (Exception ex)
            {
                //Debug.Print("Exception was cought: " + ex.Message);
                return "";
            }
        }
        #endregion

        #region Region private getSID
        private string getSID(String challengeResponse)
        {
            string augUrlPath = StringUtilities.Format("/login_sid.lua?username={0}&response={1}", user, challengeResponse);
            uri = new Uri(StringUtilities.Format("{0}://{1}{2}", useHttps ? "https" : "http", fritzUrl, augUrlPath));

            BodyHttpResponse response = new BodyHttpResponse();
            try
            {
                response = FritzHttpHelper.SendWebRequest(uri, null, 0, "GET", false, null);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    //Debug.Print("FRITZ_ERR_NO_SID");
                    return "";
                }
                sid = response.Body.Substring(response.Body.IndexOf("<SID>") + 5, response.Body.IndexOf("</SID>") - (response.Body.IndexOf("<SID>") + 5));

                return sid;
            }
            catch (Exception ex)
            {
                //Debug.Print("Exception was cought: " + ex.Message);
                return "";
            }
        }
        #endregion

        #region Region private executeRequest
        private String executeRequest(String service, String command)
        {
            String aUrlPath = StringUtilities.Format("{0}{1}", service, command);
            uri = new Uri(StringUtilities.Format("{0}://{1}{2}", useHttps ? "https" : "http", fritzUrl, aUrlPath));
            BodyHttpResponse response = new BodyHttpResponse();
            try
            {
                response = FritzHttpHelper.SendWebRequest(uri, null, 0, "GET", false, null);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return response.Body;
                }
                else
                {
                    //Debug.Print("Trying to get new session");
                    // Try to get new Session
                    if (!init())
                    {
                        Thread.Sleep(4000);
                    }
                    // returning null will through exception in command
                    return null;
                }
            }
            catch (Exception ex)
            {
                //Debug.Print("Exception was cought: " + ex.Message);
                // returning null will through exception in command
                return null;
            }
        }
        #endregion
    }
    
}
