using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Outlook;
using Microsoft.Office.Interop.Word;
using System.Collections.Generic;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace Sigwhatever
{
    class ClsOutlook
    {
        private Outlook.Application outlookApp;
        private Outlook.NameSpace nameSpace;

        public ClsOutlook()
        {
            //Get the Outlook object
            outlookApp = GetApplicationObject();
            nameSpace = outlookApp.GetNamespace("MAPI");
        }

        public void SendEmail(List<string> to, string subject, string body)
        {
            try
            {
                //Send an email
                SendEmailThroughOutlook(to, subject, body);
                Console.WriteLine("\r\nSent Email");
            }
            catch (System.Exception e)
            {
                Console.WriteLine("Error sending message: " + e.Message);
            }
            ReleaseComObject(outlookApp);
            ReleaseComObject(nameSpace);
        }

        //method to send email to outlook
        public void SendEmailThroughOutlook(List<string> toList, string subject, string body)
        {
            try
            {
                // Create the Outlook application.
                // Create a new mail item.
                Outlook.MailItem oMsg = (Outlook.MailItem)outlookApp.CreateItem(Outlook.OlItemType.olMailItem);
                Console.WriteLine("Made outlook mailitem object");

                //Subject line
                oMsg.Subject = subject;
                // Add a recipient.
                oMsg.DeleteAfterSubmit = true;
                // Set HTMLBody. 
                //add the body of the email
                oMsg.HTMLBody = body;

                Console.WriteLine("Adding recipients from list - there are " + toList.Count);
                Outlook.Recipients oRecips = (Outlook.Recipients)oMsg.Recipients;
                Console.WriteLine("======");
                // Change the recipient in the next line if necessary.
                foreach (string emailaddress in toList)
                {
                    Console.WriteLine(emailaddress);
                    if (emailaddress.Length > 2)
                    {
                        Console.WriteLine("Adding " + emailaddress + " as BCC");
                        Outlook.Recipient oRecip = (Outlook.Recipient)oRecips.Add(emailaddress);
                        oRecip.Type = (int)OlMailRecipientType.olBCC;
                        oRecip.Resolve();
                        oRecip = null;
                    }
                }
                Console.WriteLine("Done adding recipients, added {0} ", oRecips.Count);
                // Send.
                oMsg.Send();

                // Clean up.
                oRecips = null;
                oMsg = null;
                //oApp = null;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Failed to send email: " + ex.Message);
            }
        }

        public string GetExistingSig()
        {
            string signature = "";
            
            // Create the Outlook application.
            // Create a new mail item.
            Microsoft.Office.Interop.Word.Bookmark bkm = null;
            Microsoft.Office.Interop.Word.Bookmarks bkms = null;
            Microsoft.Office.Interop.Word.Document document = null;
            Outlook.Inspector inspector = null;
            Outlook.MailItem oMsg = null;

            try
            {
                oMsg = (Outlook.MailItem)outlookApp.CreateItem(Outlook.OlItemType.olMailItem);

                //Add an attachment.
                inspector = oMsg.GetInspector;
                document = (Microsoft.Office.Interop.Word.Document)inspector.WordEditor;
                document.Bookmarks.ShowHidden = true;
                bkms = document.Bookmarks;
                bkms.ShowHidden = true;
            }
            catch(System.Exception)
            {
                Console.WriteLine("[!] Couldn't get Outlook COM object");
                return null;
            }
            
            try
            {
                try
                {
                    bkm = bkms["_MailAutoSig"];
                }
                catch (System.Exception ex)
                {
                    // skip the exception 
                }
                if (bkm != null)
                {
                    Microsoft.Office.Interop.Word.Range bkmRange = bkm.Range;
                    var bkmText = bkmRange.Text;
                    if (string.IsNullOrWhiteSpace(bkmText))
                        Console.WriteLine("Signature Empty");
                    else
                        //At this point, we know there is a signature already present - the text of the signature is stored in bkmText
                        Console.WriteLine("Existing Signature is: " + bkmText);
                    signature = bkmText.ToString();
                    Marshal.ReleaseComObject(bkmRange); bkmRange = null;
                    Marshal.ReleaseComObject(bkm); bkm = null;
                }
                else
                {
                    Console.WriteLine("No Signature");
                }
            }
            catch (System.Exception ee)
            {
                Console.WriteLine("Couldn't get existing signature from email body: " + ee.Message);
            }

            try
            {
                oMsg.Close(OlInspectorClose.olDiscard);
                document.Close(WdSaveOptions.wdDoNotSaveChanges);
                inspector.Close(OlInspectorClose.olDiscard);
            }
            catch (System.Exception)
            {
                Console.WriteLine("Couldn't close Outlook inspector.");
            }

            try
            {
            }
            catch (System.Exception)
            {
                Console.WriteLine("Couldn't close Word doc from signature inspection");
            }
            return signature;
        }

        public static void ReleaseComObject(object obj)
        {
            if (obj != null)
            {
                Marshal.ReleaseComObject(obj);
                obj = null;
            }
        }

        private static Outlook.Application GetApplicationObject()
        {

            Outlook.Application application = null;

            // Check whether there is an Outlook process running.
            if (Process.GetProcessesByName("OUTLOOK").Count() > 0)
            {
                // If so, use the GetActiveObject method to obtain the process and cast it to an Application object.
                application = Marshal.GetActiveObject("Outlook.Application") as Outlook.Application;
            }
            else
            {
                // If not, create a new instance of Outlook and log on to the default profile.
                Console.WriteLine("[!] Outlook not running, you're going to need to provide creds");
                application = new Outlook.Application();
                Outlook.NameSpace nameSpace = application.GetNamespace("MAPI");
                nameSpace.Logon("", "", false, false);
                nameSpace = null;
            }
            // Return the Outlook Application object.
            return application;
        }
    }
}
