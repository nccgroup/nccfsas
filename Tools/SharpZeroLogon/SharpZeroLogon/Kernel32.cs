using System;
using System.Runtime.InteropServices;

namespace SharpZeroLogon
{
    internal class Kernel32
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool VirtualProtect(
           IntPtr lpAddress,
           uint dwSize,
           uint flNewProtect,
           out uint lpflOldProtect
        );

        [DllImport("kernel32.dll")]
        internal static extern bool ReadProcessMemory(IntPtr hProcess, long lpBaseAddress, byte[] lpBuffer, uint dwSize, ref int lpNumberOfBytesRead);

        internal struct MODULEINFO
        {
            internal IntPtr lpBaseOfDll;
            internal uint SizeOfImage;
            internal IntPtr EntryPoint;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        internal static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);
    }
}
