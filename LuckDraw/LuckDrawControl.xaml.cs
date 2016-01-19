using SensngGame.ClientSDK;
using SensngGame.ClientSDK.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    /// Interaction logic for LuckDrawControl.xaml
    /// </summary>
    public partial class LuckDrawControl : UserControl
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private GameServiceClient m_gameService;
        private int m_lastUserCount = 0;

        public LuckDrawControl()
        {
            InitializeComponent();
            m_gameService = new GameServiceClient("youkey", "wx37e46819d148d5fb", "19", "9");

            LoadQrcode();
        }

        private void LoadQrcode()
        {
            //获取签到二维码
            var fetchQrcodeTask = Task.Factory.StartNew<QrCodeData>(() =>
            {
                int tryCount = 0;
                QrCodeResult qrCodeResult;
                while (tryCount < 5)
                {
                    qrCodeResult = m_gameService.GetQrCode4LoginAsync().Result;
                    if (qrCodeResult == null)
                        continue;
                    if(qrCodeResult.Data == null)
                    {
                        log.Error("GetQrCode4LoginAsync " + qrCodeResult.ErrMessage + " " + qrCodeResult.Status);
                        continue;
                    }
                    return qrCodeResult.Data;
                    
                }
                return null;
            });

            //二维码图片 UI
            fetchQrcodeTask.ContinueWith((t) => 
            {
                QrCodeData qrcodeData = t.Result;
                if (qrcodeData == null)
                    MessageBox.Show("获取二维码失败");
                qrcodeImge.Source = new BitmapImage(new Uri(qrcodeData.QrCodeUrl));
            }, TaskScheduler.FromCurrentSynchronizationContext());

            //询轮扫人用户
            fetchQrcodeTask.ContinueWith((t) => 
            {
                QrCodeData qrcodeData = t.Result;
                if (qrcodeData == null)
                    MessageBox.Show("获取二维码失败");
                while(true)
                {
                   var userActionResult = m_gameService.FindScanQrCodeUsersAsync(qrcodeData.QrCodeId).Result;
                    if (userActionResult == null || userActionResult.Data == null)
                        continue;

                    var scanUsers = userActionResult.Data;
                    if(scanUsers.Count() != m_lastUserCount)
                    {
                        m_lastUserCount = scanUsers.Count();
                        Dispatcher.BeginInvoke((Action)(() => 
                        {
                            wall.ClearTiles();
                            
                            foreach(var usr in scanUsers)
                            {
                                wall.AddTile(usr.Id, usr.Headimgurl);
                            }
                        }));
                    }
                    Thread.Sleep(500);
                }

            });
        }

        public void ScanCheckIn(string url)
        {
            wall.AddTile(1,url);
        }      
    }

}
