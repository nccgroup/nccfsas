using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sigwhatever
{
    class HTTPCap
    {
        public static Hashtable smbSessionTable = Hashtable.Synchronized(new Hashtable());
        public static Hashtable httpSessionTable = Hashtable.Synchronized(new Hashtable());
        public static IList<string> outputList = new List<string>();
        public static bool consoleOutput = true;
        public static bool enabledInspect = false;
        public static bool enabledProxy = false;
        public static string argFileOutputDirectory = Directory.GetCurrentDirectory();
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

        public void Doit( string urlPrefix, string port, string logFile, string argChallenge)
        {
            string computerName = Environment.MachineName;
            string netbiosDomain = Environment.UserDomainName;
            string dnsDomain;

            try
            {
                dnsDomain = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
            }
            catch
            {
                dnsDomain = netbiosDomain;
            }

            Regex r = new Regex("^[A-Fa-f0-9]{16}$");
            if (!String.IsNullOrEmpty(argChallenge) && !r.IsMatch(argChallenge))
            {
                Console.WriteLine("[ERROR] Challenge is invalid");
                return;
            }

            // Print all the options
            string version = "0.913-SW";
            Console.WriteLine(String.Format("[+] HTTPCap {0} started at {1}", version, DateTime.Now.ToString("s")));
            Console.WriteLine(String.Format("[+] Encryption Password is: " + key));
            if (!String.IsNullOrEmpty(argChallenge)) Console.WriteLine(String.Format("[+] HTTP NTLM Challenge = {0}", argChallenge));
            Console.WriteLine(String.Format("[+] HTTP Authentication = {0}", true));

            // Fire HttpListener thread
            using (HttpServer srvr = new HttpServer(5, argChallenge, computerName, dnsDomain, netbiosDomain, logFile, Convert.ToInt32(port), urlPrefix))
            {
                if (srvr.Start())
                    while (true) { };
            }
        }
    }
}