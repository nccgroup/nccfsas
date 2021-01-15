using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sigwhatever
{
    public class AclHelper
    {
        public List<string> GetAcls()
        {
            List<string> lstGroups = new List<string>
            {
                "Authenticated Users",
                "Everyone",
                "Users"
            };

            List<string> permittedurls = new List<string>();

            Dictionary<string, string> dictacls = FindUrlPrefix("apps");
            foreach (KeyValuePair<string, string> a in dictacls)
            {
                if (lstGroups.Any(a.Value.Contains) && a.Value.Contains("Allow"))
                {
                    permittedurls.Add(a.Key);
                }
            }
            return permittedurls;
        }


        internal enum HTTP_SERVICE_CONFIG_QUERY_TYPE
        {
            HttpServiceConfigQueryExact,
            HttpServiceConfigQueryNext,
            HttpServiceConfigQueryMax
        }

        internal struct HTTP_SERVICE_CONFIG_URLACL_KEY
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pUrlPrefix;
        }

        internal enum HTTP_SERVICE_CONFIG_ID
        {
            HttpServiceConfigIPListenList,
            HttpServiceConfigSSLCertInfo,
            HttpServiceConfigUrlAclInfo,
            HttpServiceConfigTimeout,
            HttpServiceConfigMax
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_URLACL_QUERY
        {
            public HTTP_SERVICE_CONFIG_QUERY_TYPE QueryDesc;
            public HTTP_SERVICE_CONFIG_URLACL_KEY KeyDesc;
            public uint dwToken;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTTP_SERVICE_CONFIG_URLACL_SET
        {
            public HTTP_SERVICE_CONFIG_URLACL_KEY KeyDesc;
            public HTTP_SERVICE_CONFIG_URLACL_PARAM ParamDesc;
        }

        internal struct HTTP_SERVICE_CONFIG_URLACL_PARAM
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pStringSecurityDescriptor;
        }

        internal struct HTTPAPI_VERSION
        {
            public ushort HttpApiMajorVersion;
            public ushort HttpApiMinorVersion;
        }

        [DllImport("Httpapi.dll")]
        internal static extern uint HttpQueryServiceConfiguration(IntPtr ServiceHandle, HTTP_SERVICE_CONFIG_ID ConfigId, IntPtr pInputConfigInfo, uint InputConfigLength, IntPtr pOutputConfigInfo, uint OutputConfigInfoLength, ref uint pReturnLength, IntPtr pOverlapped);

        [DllImport("Httpapi.dll")]
        internal static extern uint HttpInitialize(HTTPAPI_VERSION Version, uint Flags, IntPtr pReserved);

        internal const uint ERROR_NO_MORE_ITEMS = 259;
        internal const uint ERROR_INSUFFICIENT_BUFFER = 122;
        internal const uint NO_ERROR = 0;
        internal const uint HTTP_INITIALIZE_CONFIG = 2;

        static AclHelper()
        {
            HTTPAPI_VERSION version = new HTTPAPI_VERSION();
            version.HttpApiMajorVersion = 1;
            version.HttpApiMinorVersion = 0;

            HttpInitialize(version, HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
        }

        private Dictionary<string, string> FindUrlPrefix(string urlPrefix)
        {
            Dictionary<string, string> acls = new Dictionary<string, string>();
            HTTP_SERVICE_CONFIG_URLACL_QUERY query = new HTTP_SERVICE_CONFIG_URLACL_QUERY();
            query.QueryDesc = HTTP_SERVICE_CONFIG_QUERY_TYPE.HttpServiceConfigQueryNext;
            IntPtr pQuery = Marshal.AllocHGlobal(Marshal.SizeOf(query));

            try
            {
                uint retval = NO_ERROR;
                for (query.dwToken = 0; ; query.dwToken++)
                {
                    Marshal.StructureToPtr(query, pQuery, false);
                    try
                    {
                        uint returnSize = 0;

                        // Get Size
                        retval = HttpQueryServiceConfiguration(IntPtr.Zero, HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo, pQuery, (uint)Marshal.SizeOf(query), IntPtr.Zero, 0, ref returnSize, IntPtr.Zero);

                        if (retval == ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }

                        if (retval != ERROR_INSUFFICIENT_BUFFER)
                        {
                            throw new Exception("HttpQueryServiceConfiguration returned unexpected error code.");
                        }

                        IntPtr pConfig = Marshal.AllocHGlobal((IntPtr)returnSize);

                        string foundPrefix;
                        string secdescriptor;
                        try
                        {
                            retval = HttpQueryServiceConfiguration(IntPtr.Zero, HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo, pQuery, (uint)Marshal.SizeOf(query), pConfig, returnSize, ref returnSize, IntPtr.Zero);
                            HTTP_SERVICE_CONFIG_URLACL_SET config = (HTTP_SERVICE_CONFIG_URLACL_SET)Marshal.PtrToStructure(pConfig, typeof(HTTP_SERVICE_CONFIG_URLACL_SET));
                            string permissions = SDDLParser.Parse(config.ParamDesc.pStringSecurityDescriptor);
                            acls.Add(config.KeyDesc.pUrlPrefix, permissions);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(pConfig);
                        }
                    }
                    finally
                    {
                        Marshal.DestroyStructure(pQuery, typeof(HTTP_SERVICE_CONFIG_URLACL_QUERY));
                    }
                }

                if (retval != ERROR_NO_MORE_ITEMS)
                {
                    throw new Exception("HttpQueryServiceConfiguration returned unexpected error code.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pQuery);
            }

            return acls;
        }
    }

    class SDDLParser
    {
        static private Dictionary<string, string> ACE_Types = null;
        static private Dictionary<string, string> ACE_Flags = null;
        static private Dictionary<string, string> Permissions = null;
        static private Dictionary<string, string> Trustee = null;

        private static void Initialize()
        {
            ACE_Types = new Dictionary<string, string>();
            ACE_Flags = new Dictionary<string, string>();
            Permissions = new Dictionary<string, string>();
            Trustee = new Dictionary<string, string>();
            #region Add ACE_Types
            ACE_Types.Add("A", "Access Allowed");
            ACE_Types.Add("D", "Access Denied");
            ACE_Types.Add("OA", "Object Access Allowed");
            ACE_Types.Add("OD", "Object Access Denied");
            ACE_Types.Add("AU", "System Audit");
            ACE_Types.Add("AL", "System Alarm");
            ACE_Types.Add("OU", "Object System Audit");
            ACE_Types.Add("OL", "Object System Alarm");
            #endregion
            #region Add ACE_Flags
            ACE_Flags.Add("CI", "Container Inherit");
            ACE_Flags.Add("OI", "Object Inherit");
            ACE_Flags.Add("NP", "No Propagate");
            ACE_Flags.Add("IO", "Inheritance Only");
            ACE_Flags.Add("ID", "Inherited");
            ACE_Flags.Add("SA", "Successful Access Audit");
            ACE_Flags.Add("FA", "Failed Access Audit");
            #endregion
            #region Add Permissions
            #region Generic Access Rights
            Permissions.Add("GA", "Generic All");
            Permissions.Add("GR", "Generic Read");
            Permissions.Add("GW", "Generic Write");
            Permissions.Add("GX", "Generic Execute");
            #endregion
            #region Directory Access Rights
            Permissions.Add("RC", "Read Permissions");
            Permissions.Add("SD", "Delete");
            Permissions.Add("WD", "Modify Permissions");
            Permissions.Add("WO", "Modify Owner");
            Permissions.Add("RP", "Read All Properties");
            Permissions.Add("WP", "Write All Properties");
            Permissions.Add("CC", "Create All Child Objects");
            Permissions.Add("DC", "Delete All Child Objects");
            Permissions.Add("LC", "List Contents");
            Permissions.Add("SW", "All Validated Writes");
            Permissions.Add("LO", "List Object");
            Permissions.Add("DT", "Delete Subtree");
            Permissions.Add("CR", "All Extended Rights");
            #endregion
            #region File Access Rights
            Permissions.Add("FA", "File All Access");
            Permissions.Add("FR", "File Generic Read");
            Permissions.Add("FW", "File Generic Write");
            Permissions.Add("FX", "File Generic Execute");
            #endregion
            #region Registry Key Access Rights
            Permissions.Add("KA", "Key All Access");
            Permissions.Add("KR", "Key Read");
            Permissions.Add("KW", "Key Write");
            Permissions.Add("KX", "Key Execute");
            #endregion
            #endregion
            #region Add Trustee's
            Trustee.Add("AO", "Account Operators");
            Trustee.Add("RU", "Alias to allow previous Windows 2000");
            Trustee.Add("AN", "Anonymous Logon");
            Trustee.Add("AU", "Authenticated Users");
            Trustee.Add("BA", "Built-in Administrators");
            Trustee.Add("BG", "Built in Guests");
            Trustee.Add("BO", "Backup Operators");
            Trustee.Add("BU", "Built-in Users");
            Trustee.Add("CA", "Certificate Server Administrators");
            Trustee.Add("CG", "Creator Group");
            Trustee.Add("CO", "Creator Owner");
            Trustee.Add("DA", "Domain Administrators");
            Trustee.Add("DC", "Domain Computers");
            Trustee.Add("DD", "Domain Controllers");
            Trustee.Add("DG", "Domain Guests");
            Trustee.Add("DU", "Domain Users");
            Trustee.Add("EA", "Enterprise Administrators");
            Trustee.Add("ED", "Enterprise Domain Controllers");
            Trustee.Add("WD", "Everyone");
            Trustee.Add("PA", "Group Policy Administrators");
            Trustee.Add("IU", "Interactively logged-on user");
            Trustee.Add("LA", "Local Administrator");
            Trustee.Add("LG", "Local Guest");
            Trustee.Add("LS", "Local Service Account");
            Trustee.Add("SY", "Local System");
            Trustee.Add("NU", "Network Logon User");
            Trustee.Add("NO", "Network Configuration Operators");
            Trustee.Add("NS", "Network Service Account");
            Trustee.Add("PO", "Printer Operators");
            Trustee.Add("PS", "Self");
            Trustee.Add("PU", "Power Users");
            Trustee.Add("RS", "RAS Servers group");
            Trustee.Add("RD", "Terminal Server Users");
            Trustee.Add("RE", "Replicator");
            Trustee.Add("RC", "Restricted Code");
            Trustee.Add("SA", "Schema Administrators");
            Trustee.Add("SO", "Server Operators");
            Trustee.Add("SU", "Service Logon User");
            #endregion
        }

        private static string friendlyTrusteeName(string trustee)
        {
            if (Trustee.Keys.Contains(trustee))
            {
                return Trustee[trustee];
            }
            else
            {
                try
                {
                    System.Security.Principal.SecurityIdentifier sid = new System.Security.Principal.SecurityIdentifier(trustee);
                    return sid.Translate(typeof(System.Security.Principal.NTAccount)).ToString();
                }
                catch (Exception)
                {
                    return trustee;
                }
            }
        }

        private static string DoParse(string subSDDL, string Separator, string Separator2)
        {
            string retval = "";
            char type = subSDDL.ToCharArray()[0];
            if (type == 'O')
            {
                string owner = subSDDL.Substring(2);
                return "Owner: " + friendlyTrusteeName(owner) + Separator;
            }
            else if (type == 'G')
            {
                string group = subSDDL.Substring(2);
                return "Group: " + friendlyTrusteeName(group) + Separator;
            }
            else if ((type == 'D') || (type == 'S'))
            {
                if (type == 'D')
                {
                    retval += "DACL" + Separator;
                }
                else
                {
                    retval += "SACL" + Separator;
                }
                string[] sections = subSDDL.Split('(');
                for (int count = 1; count < sections.Length; count++)
                {
                    retval += "# " + count.ToString() + " of " + (sections.Length - 1).ToString() + Separator;
                    string[] parts = sections[count].TrimEnd(')').Split(';');
                    retval += "";
                    if (ACE_Types.Keys.Contains(parts[0]))
                    {
                        retval += Separator2 + "Type: " + ACE_Types[parts[0]] + Separator;
                    }
                    if (ACE_Flags.Keys.Contains(parts[1]))
                    {
                        retval += Separator2 + "Inheritance: " + ACE_Flags[parts[1]] + Separator;
                    }
                    for (int count2 = 0; count2 < parts[2].Length; count2 += 2)
                    {
                        string perm = parts[2].Substring(count2, 2);
                        if (Permissions.Keys.Contains(perm))
                        {
                            if (count2 == 0)
                            {
                                retval += Separator2 + "Permissions: " + Permissions[perm];
                            }
                            else
                            {
                                retval += "|" + Permissions[perm];
                            }
                        }
                    }
                    retval += Separator;
                    retval += Separator2 + "Trustee: " + friendlyTrusteeName(parts[5]) + Separator;
                }
            }
            return retval;
        }

        public static string Parse(string SDDL)
        {
            return Parse(SDDL, "\r\n", "\t");
        }

        public static string Parse(string SDDL, string Separator, string Separator2)
        {
            string retval = "";
            if (ACE_Types == null)
            {
                Initialize();
            }
            int startindex = 0;
            int nextindex = 0;
            int first = 0;
            string section;
            while (true)
            {
                first = SDDL.IndexOf(':', nextindex) - 1;
                startindex = nextindex;
                if (first < 0)
                {
                    break;
                }
                if (first != 0)
                {
                    section = SDDL.Substring(startindex - 2, first - startindex + 2);
                    retval += DoParse(section, Separator, Separator2);
                }
                nextindex = first + 2;
            }
            section = SDDL.Substring(startindex - 2);
            retval += DoParse(section, Separator, Separator2);
            return retval;
        }
    }
}