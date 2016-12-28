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
                //显示奖品
                luck.ShowAward(e.Key - Key.D0);
            }
            else if(e.Key == Key.Enter)
            {
                //开始
                luck.Begin();
            }
            else if(e.Key == Key.F1)
            {
                //弹幕
                luck.bulletCurtain.Toggle();
            }
            else if(e.Key == Key.Z)
            {
                //显示大二维码
                luck.bigQrcode.Visibility = luck.bigQrcode.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
            else if(e.Key == Key.OemPlus)
            {
                //"+"中奖人数
                luck.AddLuckOne();
            }
            else if(e.Key == Key.OemMinus)
            {
                //"-"中奖人数
                luck.wall.RemoveCubic();
            }
            else if(e.Key == Key.R)
            {
                luck.wall.Roll();
            } if(e.Key == Key.F5)
            {
                //刷新
                luck.Reset();
            }
        }

    }

    
}
