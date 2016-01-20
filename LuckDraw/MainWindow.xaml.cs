using System;
using System.Collections.Generic;
using System.IO;
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

namespace LuckDraw
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        int i = 0;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.Key == Key.D1)
            {
                luck.ShowAward();
            }
            else if(e.Key == Key.D2)
            {

            }
            else if(e.Key == Key.D3)
            {

            }
            else if(e.Key == Key.A)
            {
                if (i >= 6) return;
                var appRoot = AppDomain.CurrentDomain.BaseDirectory;
                var usrHeadDir = appRoot + "userhead";

                var pngs = Directory.GetFiles(usrHeadDir).Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")).ToArray();
                luck.ScanCheckIn(pngs[i]);
                i++;
            }
            else if(e.Key == Key.Enter)
            {
                luck.Begin();
            }
        }

    }

    
}
