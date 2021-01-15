using System;
using System.IO;
using System.Linq;
using System.Net;
using NetFwTypeLib;
using Microsoft.Win32;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Sigwhatever
{
    class Util
    {
        public bool HasWriteAccessToFolder()
        {
            try
            {
                string folderPath = Directory.GetCurrentDirectory();
                Console.WriteLine($"[LOG FILE LOCATION] Current directory is {folderPath}");
                // Attempt to get a list of security permissions from the folder. 
                // This will raise an exception if the path is read only or do not have access to view the permissions. 
                System.Security.AccessControl.DirectorySecurity ds = Directory.GetAccessControl(folderPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public string ListInternetSettings()
        {
            string returnme = "";
            // lists user/system internet settings, including default proxy info

            Dictionary<string, object> proxySettings = GetRegValues("HKCU", "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
            Console.WriteLine("\r\n=== HKCU Internet Settings ===\r\n");
            if ((proxySettings != null) && (proxySettings.Count != 0))
            {
                foreach (KeyValuePair<string, object> kvp in proxySettings)
                {
                    Console.WriteLine("  {0,30} : {1}", kvp.Key, kvp.Value);
                    returnme += kvp.Key + " " + kvp.Value;
                }
            }

            Dictionary<string, object> proxySettings2 = GetRegValues("HKLM", "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
            Console.WriteLine("\r\n=== HKLM Internet Settings ===\r\n");
            if ((proxySettings2 != null) && (proxySettings2.Count != 0))
            {
                foreach (KeyValuePair<string, object> kvp in proxySettings2)
                {
                    Console.WriteLine("  {0,30} : {1}", kvp.Key, kvp.Value);
                    returnme += kvp.Key + " " + kvp.Value;
                }
            }
            Console.WriteLine();
            return returnme;
        }

        public bool PortFree(int port)
        {
            bool inUse = false;
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            }
            return inUse;
        }

        public bool CheckFirewall(int port)
        {
            Console.WriteLine("[*] Checking firewall status...");

            Util u = new Util();
            List<int> ports = u.GetFwInbound();

            if (!ports.Contains(port))
            {
                //If we got here, then the firewall is on and the specified port on the command line does not match an inbound rule
                Console.WriteLine("[-] The firewall is on and the port you specified does not match any inbound firewal rules.");

                if (ports.Count > 0 && ports[0] != 0)
                {
                    Console.WriteLine("[*] Enumerated viable ports are:\n");
                    foreach (int p in ports)
                    {
                        Console.WriteLine($" *:{p}");
                    }
                    Console.WriteLine();
                }
                return false;
            }
            Console.WriteLine($"[+] Port {port} is allowed");
            return true;
        }

        public static bool IsSystemException(string port)
        {
            // Check if there is an exception for the System process
            Type NetFwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
            INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);

            if (!mgr.LocalPolicy.CurrentProfile.FirewallEnabled)
                return false;

            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);

            List<INetFwRule> RuleList = fwPolicy2.Rules.Cast<INetFwRule>().ToList();
            foreach (INetFwRule rule in RuleList)
            {
                if (rule.Direction == NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN)
                {
                    if (rule.ApplicationName != null && rule.ApplicationName.ToUpper() == "SYSTEM")
                    {
                        if (rule.LocalPorts != null && rule.Enabled == true)
                        {
                            string[] localports = rule.LocalPorts.Split(',');
                            foreach (string portdef in localports)
                            {
                                if (portdef == port)
                                    return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public List<int> GetFwInbound()
        {
            List<int> ports = new List<int>();
            Console.WriteLine("[+] Enumerating potential ports to listen on...");

            Type NetFwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
            INetFwMgr mgr = (INetFwMgr)Activator.CreateInstance(NetFwMgrType);
            bool Firewallenabled = mgr.LocalPolicy.CurrentProfile.FirewallEnabled;
            if (!Firewallenabled)
            {
                Console.WriteLine("[YAY] No firewall enabled, bind to anything!");
                ports.Add(0);
                return ports;
            }
            else
            {
                Console.WriteLine("[!] Firewall enabled, looking for a permissive rule");
            }

            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);
            List<INetFwRule> RuleList = fwPolicy2.Rules.Cast<INetFwRule>().ToList();
            foreach (INetFwRule rule in RuleList)
            {
                if (rule.Direction == NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN)
                {
                    if (rule.ApplicationName == null)
                    {
                        if (rule.LocalPorts != null && rule.Enabled == true)
                        {
                            Console.WriteLine($"\n=========\r\nName: {rule.Name}\nDescription: {rule.Description}\nDirection: {rule.Direction} \nLocal Ports: {rule.LocalPorts}\nLocal Addresses: {rule.LocalAddresses}\n");
                            string[] localports = rule.LocalPorts.Split(',');
                            foreach (string portdef in localports)
                            {
                                if (portdef.Contains('-'))
                                {
                                    try
                                    {
                                        string[] portrange = portdef.Split('-');
                                        int n1 = Convert.ToInt32(portrange[0]);
                                        int n2 = Convert.ToInt32(portrange[1]);
                                        for (int i = n1; i <= n2; i++)
                                        {
                                            ports.Add(i);
                                        }
                                    }
                                    catch (Exception e)
                                    { }
                                }
                                else
                                {
                                    try
                                    {
                                        ports.Add(Convert.ToInt32(portdef));
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return ports;
        }

        private static Dictionary<string, object> GetRegValues(string hive, string path)
        {
            // returns all registry values under the specified path in the specified hive (HKLM/HKCU)
            Dictionary<string, object> keyValuePairs = null;
            try
            {
                if (hive == "HKCU")
                {
                    using (var regKeyValues = Registry.CurrentUser.OpenSubKey(path))
                    {
                        if (regKeyValues != null)
                        {
                            var valueNames = regKeyValues.GetValueNames();
                            keyValuePairs = valueNames.ToDictionary(name => name, regKeyValues.GetValue);
                        }
                    }
                }
                else if (hive == "HKU")
                {
                    using (var regKeyValues = Registry.Users.OpenSubKey(path))
                    {
                        if (regKeyValues != null)
                        {
                            var valueNames = regKeyValues.GetValueNames();
                            keyValuePairs = valueNames.ToDictionary(name => name, regKeyValues.GetValue);
                        }
                    }
                }
                else
                {
                    using (var regKeyValues = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (regKeyValues != null)
                        {
                            var valueNames = regKeyValues.GetValueNames();
                            keyValuePairs = valueNames.ToDictionary(name => name, regKeyValues.GetValue);
                        }
                    }
                }
                return keyValuePairs;
            }
            catch
            {
                return null;
            }
        }

        public static string HexStringToString(string hexString)
        {
            string[] stringArray = hexString.Split('-');
            string stringConverted = "";

            foreach (string character in stringArray)
            {
                stringConverted += new String(Convert.ToChar(Convert.ToInt16(character, 16)), 1);
            }

            return stringConverted;
        }

        public static uint UInt16DataLength(int start, byte[] field)
        {
            byte[] fieldExtract = new byte[2];

            if (field.Length > start + 2)
            {
                System.Buffer.BlockCopy(field, start, fieldExtract, 0, 2);
            }

            return BitConverter.ToUInt16(fieldExtract, 0);
        }

        public static uint UInt32DataLength(int start, byte[] field)
        {
            byte[] fieldExtract = new byte[4];
            System.Buffer.BlockCopy(field, start, fieldExtract, 0, 4);
            return BitConverter.ToUInt32(fieldExtract, 0);
        }

        public static string DataToString(int start, int length, byte[] field)
        {
            string payloadConverted = "";

            if (length > 0)
            {
                byte[] fieldExtract = new byte[length - 1];
                Buffer.BlockCopy(field, start, fieldExtract, 0, fieldExtract.Length);
                string payload = BitConverter.ToString(fieldExtract);
                payload = payload.Replace("-00", String.Empty);
                string[] payloadArray = payload.Split('-');

                foreach (string character in payloadArray)
                {
                    payloadConverted += new System.String(Convert.ToChar(Convert.ToInt16(character, 16)), 1);
                }
            }
            return payloadConverted;
        }

        public static string GetLocalIPAddress(string ipVersion)
        {

            List<string> ipAddressList = new List<string>();
            AddressFamily addressFamily;

            if (String.Equals(ipVersion, "IPv4"))
            {
                addressFamily = AddressFamily.InterNetwork;
            }
            else
            {
                addressFamily = AddressFamily.InterNetworkV6;
            }

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet && networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == addressFamily)
                        {
                            ipAddressList.Add(ip.Address.ToString());
                        }
                    }
                }
            }
            return ipAddressList.FirstOrDefault();
        }
    }
}