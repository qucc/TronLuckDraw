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
            log4net.Config.XmlConfigurator.Configure();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                luck.ShowAward(e.Key - Key.D0);
            }
            else if(e.Key == Key.Enter)
            {
                luck.Begin();
            }
            else if(e.Key == Key.F1)
            {
                luck.bulletCurtain.Toggle();
            }
            else if(e.Key == Key.Z)
            {
                luck.bigQrcode.Visibility = luck.bigQrcode.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

    }

    
}
