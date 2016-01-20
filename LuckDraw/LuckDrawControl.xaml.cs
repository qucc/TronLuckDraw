using SensngGame.ClientSDK;
using SensngGame.ClientSDK.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
using System.Windows.Threading;

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
        private Dictionary<int, WhiteListItem> m_whiteList = null;
        private AppState m_currentState = AppState.ScanQrcode;
        private AwardData m_currentAward = null;
        private DispatcherTimer m_timer = new DispatcherTimer();
        private Random m_rnd = new Random();


        public LuckDrawControl()
        {
            InitializeComponent();
            m_gameService = new GameServiceClient("j;lajdf;jaiuefjf", "wx37e46819d148d5fb", "19", "9");
            m_timer.Interval = TimeSpan.FromMilliseconds(100);
            m_timer.Tick += Tick;
            //LoadQrcode();
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
            
            awardImage.Source = new BitmapImage(new Uri(m_currentAward.AwardImagePath));
            awardText.Text = m_currentAward.Name;
            GoToState(AppState.ShowAward);
        }

        public void Begin()
        {
            if(m_currentState == AppState.ShowAward)
            {
                m_timer.Start();
                GoToState(AppState.Gaming);
            }
            else if (m_currentState == AppState.Gaming)
            {
                m_timer.Stop();
                UserInfoData winner = null;
                if(m_whiteList != null && m_whiteList.ContainsKey(m_currentAward.Id))
                {
                    var whiteUsersIds = m_whiteList[m_currentAward.Id].Users;
                    var candidateIds = whiteUsersIds.Intersect(m_scanUsers.Select(u => u.Id));
                    if(candidateIds.Count() > 0)
                    {
                        var winnerId = m_rnd.Next(candidateIds.Count());
                        winner = m_scanUsers.First(u => u.Id == winnerId);
                    }
                }
                if(winner == null)
                {
                    int  winnerIndex = m_rnd.Next(m_scanUsers.Count());
                    winner = m_scanUsers[winnerIndex];
                }
                SetWinnerImage(winner);
                GoToState(AppState.ShowWinner);
            }
        }

        int m_tickIndex = 0;
        private void Tick(object sender, EventArgs e)
        {
            if (m_tickIndex >= m_scanUsers.Count)
                m_tickIndex = 0;
            SetWinnerImage(m_scanUsers[m_tickIndex]);
            m_tickIndex++;
        }

        private void SetWinnerImage(UserInfoData winner)
        {
            winnerImage.Source = new BitmapImage(new Uri(winner.Headimgurl, UriKind.Absolute));
            winnerName.Text = winner.Nickname;
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
                m_whiteList = new Dictionary<int, WhiteListItem>();
                foreach(var u in whiteList)
                {
                    var awardIds = u.AwardSeqs.Split(new char[] { ','});
                    foreach(var awardId in awardIds)
                    {
                        int id = int.Parse(awardId);
                        if (!m_whiteList.ContainsKey(id))
                        {
                            m_whiteList.Add(id, new WhiteListItem());
                        }
                        m_whiteList[id].Users.Add(u.Id);
                    }
                }

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
                    var appRoot = AppDomain.CurrentDomain.BaseDirectory;
                    var awardDir = appRoot + "award/";
                    if (!Directory.Exists(awardDir)) Directory.CreateDirectory(awardDir);

                    foreach(var award in awardsResult.Data)
                    {
                        if (award.AwardImagePath == null)
                        {
                            log.Error("奖品图片为空");
                            continue;
                        }
                        award.AwardImagePath = GameServiceClient.ServerBase + award.AwardImagePath;
                        var dest = awardDir + award.AwardImagePath.GetHashCode() + ".png";
                        if (File.Exists(dest))
                        {
                            award.AwardImagePath = dest;
                            continue;
                        }
                        if(DownloadImage(award.AwardImagePath, dest))
                        {
                            award.AwardImagePath = dest;
                        }
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
                        WebClient webClient = new WebClient();
                        var appRoot = AppDomain.CurrentDomain.BaseDirectory;
                        var headDir = appRoot + "head/";
                        if (!Directory.Exists(headDir)) Directory.CreateDirectory(headDir);

                        foreach (var u in scanUsers)
                        {
                            var targetPath = headDir + u.Headimgurl.GetHashCode() + ".jpg";
                            if (File.Exists(targetPath))
                            {
                                u.Headimgurl = targetPath;
                                continue;
                            }

                            try
                            {
                                var tmp = targetPath + ".downloading";
                                webClient.DownloadFile(u.Headimgurl, tmp);
                                File.Move(tmp, targetPath);
                                u.Headimgurl = targetPath;
                            }
                            catch (Exception) { }
                        }

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

        private bool DownloadImage(string url, string dest)
        {
            WebClient webClient = new WebClient();
            try
            {
                var tmp = dest + ".downloading";
                webClient.DownloadFile(url, tmp);
                File.Move(tmp, dest);
                return true;
            }
            catch (Exception) { }
            return false;
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
            usersCountText.Text = m_scanUsers.Count.ToString();
        }      

        private void GoToState(AppState state)
        {
            qrcodePanel.Visibility = state == AppState.ScanQrcode ? Visibility.Visible : Visibility.Hidden;
            awardPanel.Visibility = state != AppState.ScanQrcode ? Visibility.Visible : Visibility.Hidden;
            winnerPanel.Visibility = state == AppState.ShowWinner || state == AppState.Gaming ? Visibility.Visible : Visibility.Hidden;
            m_currentState = state;
        }
    }

    enum AppState
    {
        ScanQrcode,
        ShowAward,
        Gaming,
        ShowWinner
    }

    class WhiteListItem 
    {
        public WhiteListItem()
        {
            Users = new List<int>();
        }
        public List<int> Users { get; set; }
    }

}
