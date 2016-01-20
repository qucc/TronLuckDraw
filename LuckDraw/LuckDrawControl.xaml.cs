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
        private List<UserActionData> m_scanUsers = null;
        private List<AwardData> m_awards = null;
        private List<WhiteUserData> m_whiteList = null;
        private bool m_isShowAward = false;
        private AwardData m_currentAward = null;

        public LuckDrawControl()
        {
            InitializeComponent();
            m_gameService = new GameServiceClient("j;lajdf;jaiuefjf", "wx37e46819d148d5fb", "19", "9");

            LoadQrcode();
            LoadAwardList();
            LoadWhiteList();
        }

        public void ShowAward()
        {
            if(m_awards.Count() == 0)
            {
                MessageBox.Show("奖品已抽完");
                return;
            }

            m_currentAward = m_awards.FirstOrDefault();
            m_currentAward.ActualQty--;
            if (m_currentAward.ActualQty == 0)
                m_awards.Remove(m_currentAward);
            
            awardImage.Source = new BitmapImage(new Uri(GameServiceClient.ServerBase + m_currentAward.AwardImagePath));
            awardText.Text = m_currentAward.Name;
            m_isShowAward = true;
        }

        public void Begin()
        {
            if(m_isShowAward)
            {
                Random rnd = new Random();
                int index = rnd.Next(0, m_scanUsers.Count());
                var winner = m_scanUsers[index];
                winnerImage.Source = new BitmapImage(new Uri(winner.Headimgurl, UriKind.Absolute));
                winnerName.Text = winner.Nickname;
                wall.StartAnimate(awardImage.Source as BitmapSource);
                Task.Factory.StartNew(() => {
                  var userAwardResult = m_gameService.WinAwardByUser(m_currentAward.Id.ToString(), winner.Id.ToString()).Result;

                });
            }
        }

        private void LoadWhiteList()
        {
            var fetchWhiteListTask = Task.Factory.StartNew<List<WhiteUserData>>(() => 
            {
                WhiteUsersResult whiteUserResult = null;
                int tryCount = 0;
                while (tryCount < 5)
                {
                    whiteUserResult = m_gameService.GetActivityWhiteListUsers().Result;
                    if (whiteUserResult == null)
                        continue;
                    if(whiteUserResult.Data == null)
                    {
                        log.Error("GetActivityWhiteListUser " + whiteUserResult.ErrMessage);
                        continue;
                    }
                    return whiteUserResult.Data;
                }
                return null;
            });
            fetchWhiteListTask.ContinueWith((t) =>
            {
                List<WhiteUserData> whiteList = t.Result;
                if(whiteList == null)
                {
                    MessageBox.Show("获取白名单失败");
                    return;
                }
                m_whiteList = whiteList;

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void LoadAwardList()
        {
           var fetchAwardListTask =  Task.Factory.StartNew<List<AwardData>>(() => 
            {
                AwardsResult awardsResult;
                int tryCount=0 ;
                while (tryCount < 5)
                {
                    awardsResult = m_gameService.GetAwardsByActivity().Result;
                    tryCount++;
                    if (awardsResult == null)
                        continue;
                    if(awardsResult.Data == null)
                    {
                        log.Error("GetAwardsByActivity" + awardsResult.ErrMessage);
                        continue;
                    }
                    return awardsResult.Data;
                }
                return null;
            });

            fetchAwardListTask.ContinueWith((t) => 
            {
                List<AwardData> awardDataList = t.Result;
                if (awardDataList == null)
                {
                    MessageBox.Show("获取奖器列表失败");
                    return;
                }
                m_awards = awardDataList;

            }, TaskScheduler.FromCurrentSynchronizationContext());
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
                    tryCount++;
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
                    Thread.Sleep(1000);
                    var userActionResult = m_gameService.FindScanQrCodeUsersAsync(qrcodeData.QrCodeId).Result;
                    if (userActionResult == null)
                        continue;
                    Console.WriteLine("FindScanQrCodeUsersAsync " + userActionResult.ErrMessage);
                    if (userActionResult.Data == null)
                    {
                        continue;
                    }

                    var scanUsers = userActionResult.Data;
                    if(scanUsers.Count() != m_lastUserCount)
                    {
                        m_lastUserCount = scanUsers.Count();
                        Dispatcher.BeginInvoke((Action)(() => 
                        {
                            wall.ClearTiles();
                            usersCountText.Text = m_lastUserCount.ToString();
                            m_scanUsers = scanUsers;
                            foreach (var usr in scanUsers)
                            {
                                wall.AddTile(usr.Id, usr.Headimgurl);
                            }
                        }));
                    }
                }

            });
        }

        public void ScanCheckIn(string url)
        {
            wall.AddTile(1,url);
            if (m_scanUsers == null)
                m_scanUsers = new List<UserActionData>();
            m_scanUsers.Add(new UserActionData {
                Headimgurl = url,
                Nickname = "Name " + url.GetHashCode()
            });
        }      
    }

}
