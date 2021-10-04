
using System;
using Microsoft.SPOT;
using System.Net;
using Microsoft.SPOT.Net.Security;
using System.IO;
using System.Collections;
using System.Text;
using System.Threading;
//using RoSchmi.Net.Azure.Storage;

namespace RoSchmi.Net.Fritzbox
{
    
        /// <summary>
        /// A common helper class for HTTP access to Windows Azure Storage
        /// </summary>
        public static class FritzHttpHelper
        {
            /// <summary>
            /// Sends a Web Request prepared for Fritzbox
            /// </summary>
            /// <param name="url"></param>
            /// <param name="authHeader"></param>
            /// <param name="dateHeader"></param>
            /// <param name="versionHeader"></param>
            /// <param name="fileBytes"></param>
            /// <param name="contentLength"></param>
            /// <param name="httpVerb"></param>
            /// <param name="expect100Continue"></param>
            /// <param name="Accept-Type"></param>
            /// <param name="additionalHeaders"></param>
            /// <returns></returns>
            ///

            private static Object theLock1 = new Object();


            private static bool _fiddlerIsAttached = false;
            private static IPAddress _fiddlerIP = null;
            private static int _fiddlerPort = 8888;


            #region "Debugging"
            private static DebugMode _debug = DebugMode.NoDebug;
            private static DebugLevel _debug_level = DebugLevel.DebugErrors;

            /// <summary>
            /// Represents the debug mode.
            /// </summary>
            public enum DebugMode
            {
                /// <summary>
                /// Use no debugging
                /// </summary>
                NoDebug,

                /// <summary>
                /// Report debugging to Visual Studio debug output
                /// </summary>
                StandardDebug,

                /// <summary>
                /// Re-direct debugging to a given serial port.
                /// Console Debugging
                /// </summary>
                SerialDebug
            };

            /// <summary>
            /// Represents the debug level.
            /// </summary>
            public enum DebugLevel
            {
                /// <summary>
                /// Only debug errors.
                /// </summary>
                DebugErrors,
                /// <summary>
                /// Debug everything.
                /// </summary>
                DebugErrorsPlusMessages,
                /// <summary>
                /// Debug everything.
                /// </summary>
                DebugAll
            };


            private static void _Print_Debug(string message)
            {
                lock (theLock1)
                {
                    switch (_debug)
                    {
                        //Do nothing
                        case DebugMode.NoDebug:
                            break;

                        //Output Debugging info to the serial port
                        case DebugMode.SerialDebug:
                            //Convert the message to bytes
                            
                           // byte[] message_buffer = System.Text.Encoding.UTF8.GetBytes(message);
                           // _debug_port.Write(message_buffer,0,message_buffer.Length);


                            
                            break;

                        //Print message to the standard debug output
                        case DebugMode.StandardDebug:
                            Debug.Print(message);
                            break;
                    }
                }
            }
            #endregion
            /// <summary>
            /// Set the debugging level.
            /// </summary>
            /// <param name="Debug_Level">The debug level</param>
            public static void SetDebugLevel(DebugLevel Debug_Level)
            {
                lock (theLock1)
                { 
                    _debug_level = Debug_Level;
                }
            }
            /// <summary>
            /// Set the debugging mode.
            /// </summary>
            /// <param name="Debug_Level">The debug level</param>
            public static void SetDebugMode(DebugMode Debug_Mode)
            {
                /*
                lock (theLock1)
                {
                    _debug = Debug_Mode;
                }
                */
            }



            public static void AttachFiddler(bool pfiddlerIsAttached, IPAddress pfiddlerIP, int pfiddlerPort)
            {
                lock (theLock1)
                {
                    _fiddlerIsAttached = pfiddlerIsAttached;
                    _fiddlerIP = pfiddlerIP;
                    _fiddlerPort = pfiddlerPort;
                }
            }

            //public static BasicHttpResponse SendWebRequest(Uri url, string authHeader, string dateHeader, string versionHeader, byte[] payload = null, int contentLength = 0, string httpVerb = "GET", bool expect100Continue = false, string acceptType = "application/json;odata=minimalmetadata", Hashtable additionalHeaders = null)
            public static BodyHttpResponse SendWebRequest(Uri url, byte[] payload = null, int contentLength = 0, string httpVerb = "GET", bool expect100Continue = false, Hashtable additionalHeaders = null)
            {
                string responseBody = "";
                HttpStatusCode responseStatusCode = HttpStatusCode.Ambiguous;
                try
                {
                    HttpWebResponse response = null;
                    
                    HttpWebRequest request = null;
                    try
                    {
                        request = PrepareRequest(url, payload, contentLength, httpVerb, expect100Continue, additionalHeaders);
                        if (request != null)
                        {
                            // Assign the certificates. The value must not be null if the
                            // connection is HTTPS.

                            //request.HttpsAuthentCerts = TableClient.caCerts;

                            //HttpWebRequest.DefaultWebProxy = new WebProxy("4.2.2.2", true);

                            // Evtl. set request.KeepAlive to use a persistent connection.
                            request.KeepAlive = false;
                            request.Timeout = 100000;               // timeout 100 sec = standard
                            request.ReadWriteTimeout = 100000;      // timeout 100 sec, standard = 300
                            lock (theLock1)
                            {
                                if (_debug_level == DebugLevel.DebugErrorsPlusMessages || _debug_level == DebugLevel.DebugAll)
                                {
                                    _Print_Debug("Time of request (no DLST): " + DateTime.Now);
                                    _Print_Debug("Url: " + url.AbsoluteUri);
                                }
                            }
                            // This is needed since there is an exception if the GetRequestStream method is called with GET or HEAD
                            if ((httpVerb != "GET") && (httpVerb != "HEAD"))
                            {
                                using (Stream requestStream = request.GetRequestStream())
                                {
                                    requestStream.Write(payload, 0, contentLength);
                                }
                            }
                                response = (HttpWebResponse)request.GetResponse();

                                if (response != null)
                                {
                                    
                                    responseStatusCode = response.StatusCode;
                                    Stream dataStream = response.GetResponseStream();

                                                                   

                                    StreamReader reader = new StreamReader(dataStream);

                                    char[] buf = new char[500];
                                    char[] charBuf = new char[1];
                                    
                                    int actIndex = 0;
                                    int readCnt = 0;

                                    while (reader.Peek() != -1 )
                                    {
                                        readCnt = reader.Read(charBuf, 0, 1);
                                        Array.Copy(charBuf, 0, buf, actIndex, 1);
                                        actIndex++;
                                    }
                                    

                                    responseBody = new string(buf, 0, actIndex);

                                    
                                    int dummy34 = 1;
                                    //Report all incomming data to the debug
                                    lock (theLock1)
                                    {
                                        if (_debug_level == DebugLevel.DebugAll)
                                        {
                                            _Print_Debug(responseBody);
                                        }
                                    }
                                    reader.Close();
                                    if (response.StatusCode == HttpStatusCode.Forbidden)
                                    //if ((response.StatusCode == HttpStatusCode.Forbidden) || (response.StatusCode == HttpStatusCode.NotFound))
                                    {
                                        lock (theLock1)
                                        {
                                            _Print_Debug("Problem with signature. Check next debug statement for stack");

                                            throw new WebException("Forbidden", null, WebExceptionStatus.TrustFailure, response);
                                        }
                                    }
                                    response.Close();
                                    if (responseBody == null)
                                        responseBody = "No body content";

                                    //_Print_Debug(responseBody);
                                    return new BodyHttpResponse() { Body = responseBody, StatusCode = responseStatusCode };

                                }
                                else
                                {
                                    return new BodyHttpResponse() {Body = responseBody, StatusCode = responseStatusCode };
                                }
                            }
                            else
                            {
                                lock (theLock1)
                                {
                                    _Print_Debug("Failure: Request is null");
                                }
                                return new BodyHttpResponse() { Body = responseBody, StatusCode = responseStatusCode };
                            }
                    }
                    catch (WebException ex)
                    {
                        lock (theLock1)
                        {
                            _Print_Debug("An error occured. Status code:" + ((HttpWebResponse)ex.Response).StatusCode);
                        }
                        responseStatusCode = ((HttpWebResponse)ex.Response).StatusCode;
                        using (Stream stream = ex.Response.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(stream))
                            {
                                StringBuilder sB = new StringBuilder("");
                                Char[] chunk = new char[20];

                                while (sr.Peek() > -1 )
                                {
                                    int readBytes = sr.Read(chunk, 0, chunk.Length);
                                    sB.Append(chunk, 0, readBytes);
                                }
                                responseBody = sB.ToString();
                                lock (theLock1)
                                {
                                    _Print_Debug(responseBody);
                                }

                                /*
                                var s = sr.ReadToEnd();
                                lock (theLock1)
                                {
                                    _Print_Debug(s);
                                }
                                responseBody = s;
                                */

                                return new BodyHttpResponse() { Body = responseBody, StatusCode = responseStatusCode };
                            }
                        }
                    }
                    
                    catch (Exception ex2)
                    {
                        lock (theLock1)
                        {
                            _Print_Debug("Exception in HttpWebRequest.GetResponse(): " + ex2.Message);
                            _Print_Debug("ETag: " +  "null" + " Body: " + responseBody + " StatusCode: " + responseStatusCode);
                        }
                        return new BodyHttpResponse() { Body = responseBody, StatusCode = responseStatusCode };
                    }
                    finally
                    {
                        if (response != null)
                        {
                            response.Dispose();
                        }
                        if (request != null)
                        {
                            request.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (theLock1)
                    {
                        _Print_Debug("Exception in HttpWebRequest: " + ex.Message);
                    }
                    return new BodyHttpResponse() { Body = responseBody, StatusCode = responseStatusCode };
                }
            }

            /// <summary>
            /// Prepares a HttpWebRequest with required headers of x-ms-date, x-ms-version, Authorization and others
            /// </summary>
            /// <param name="url"></param>    
            /// <param name="fileBytes"></param>
            /// <param name="contentLength"></param>
            /// <param name="httpVerb"></param>
            /// <param name="expect100Continue"></param>
            /// <param name="additionalHeaders"></param>
            /// <returns></returns>
            private static HttpWebRequest PrepareRequest(Uri url, byte[] fileBytes, int contentLength, string httpVerb, bool expect100Continue = false, Hashtable additionalHeaders = null)
            {
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)WebRequest.Create(url);
                request.Method = httpVerb;
                request.ContentLength = contentLength;
                request.UserAgent = "RsNetmfHttpClient";
                if (expect100Continue)
                {
                    request.Expect = "100-continue";
                }
                if (additionalHeaders != null)
                {
                    foreach (var additionalHeader in additionalHeaders.Keys)
                    {
                        request.Headers.Add(additionalHeader.ToString(), additionalHeaders[additionalHeader].ToString());
                    }
                }

                //*******************************************************
                // To use Fiddler as WebProxy include this code segment
                // Use the local IP-Address of the PC where Fiddler is running
                // See here how to configurate Fiddler; -http://blog.devmobile.co.nz/2013/01/09/netmf-http-debugging-with-fiddler
                lock (theLock1)
                {
                    if (_fiddlerIsAttached)
                    {
                        request.Proxy = new WebProxy(_fiddlerIP.ToString(), _fiddlerPort);
                    }
                }
                //**********

                //PrintKeysAndValues(request.Headers);

                return request;
            }

            public static void PrintKeysAndValues(WebHeaderCollection myHT)
            {
                lock (theLock1)
                {
                    string[] allKeys = myHT.AllKeys;
                    _Print_Debug("\r\nThe request was sent with the following headers");
                    foreach (string Key in allKeys)
                    {
                        _Print_Debug(Key + ":");
                    }
                    _Print_Debug("\r\n");
                }
            }

            public static void PrintKeysAndValues(Hashtable myHT)
            {
                lock (theLock1)
                {
                    _Print_Debug("\r\nThe request was sent with the following headers");
                    foreach (DictionaryEntry de in myHT)
                    {
                        _Print_Debug(de.Key + ":" + de.Value);
                    }
                    _Print_Debug("\r\n");
                }

            }
            }
        }


