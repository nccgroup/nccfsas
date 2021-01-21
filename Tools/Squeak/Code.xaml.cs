using System;
using System.Collections.Generic;
using System.Linq;
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
using System.IO;

namespace Squeak
{
    /// <summary>
    /// Interaction logic for Code.xaml
    /// </summary>
    public partial class Code : Page
    {
        public Code()
        {
            InitializeComponent();
            
        }

        private void CodeSave_Click(object sender, RoutedEventArgs e)
        {
            bool save = true;
            string newcode = RTB.Text;
            if(!newcode.Contains("[HEX]"))
            {
                MessageBox.Show("Code does not contain the [HEX] placeholder, please put this in.");
                save = false;
            }
           
            if(save)
            {
                File.WriteAllText("clrcode.cs", newcode);
            }
           
            

        }

        void Code_Loaded(object sender, RoutedEventArgs e)
        {
           string code = File.ReadAllText("clrcode.cs");
            // foreach (string line in code)
            RTB.CurrentHighlighter = AurelienRibon.Ui.SyntaxHighlightBox.HighlighterManager.Instance.Highlighters["CSharp"];
            RTB.Text = code;
            
            
        }

      
    }
}
