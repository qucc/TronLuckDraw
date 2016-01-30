using Newtonsoft.Json;
using SensngGame.ClientSDK;
using SensngGame.ClientSDK.Contract;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        private bool m_longPulling = false;

        private List<UserActionData> m_scanUsers = null;
        private List<AwardData> m_awards = null;
        
        private AppState m_currentState = AppState.ScanQrcode;
        private AwardData m_currentAward = null;
        private List<int> m_candidateIds = null;
        private DispatcherTimer m_timer = new DispatcherTimer();
        private Random m_rnd = new Random();
        private readonly string Activity_ID = ConfigurationManager.AppSettings["ActivityId"].ToString();

        public LuckDrawControl()
        {
            InitializeComponent();
            m_gameService = new GameServiceClient("j;lajdf;jaiuefjf", "wx37e46819d148d5fb", "19", Activity_ID);
            m_timer.Interval = TimeSpan.FromMilliseconds(100);
            m_timer.Tick += Tick;
            bulletCurtain.SetGameServiceClient(m_gameService);
            LoadQrcode();
            LoadAwardList();
    
        }


        public void ShowAward(int awardLevel)
        {
            if(m_awards.Count() == 0)
            {
                MessageBox.Show("所有奖品已抽完");
                return;
            }
            m_currentAward = m_awards.Where(a => a.AwardSeq == awardLevel).FirstOrDefault();
            if(m_currentAward == null)
            {
                MessageBox.Show("该奖项不存在");
                return;
            }
            m_currentAward = m_awards.Where(a => a.AwardSeq == awardLevel && a.ActualQty < a.PlanQty).FirstOrDefault();

            if (m_currentAward == null)
            {
                MessageBox.Show("该奖品已抽完");
                return;
            }
            
            if(m_currentAward.AwardImagePath != null)
                 awardImage.Source = new BitmapImage(new Uri(m_currentAward.AwardImagePath));
            awardText.Text = m_currentAward.Name;
            awardNameText.Text = m_currentAward.AwardProduct;
            GoToState(AppState.ShowAward);
        }

        public void Begin()
        {
            if(m_currentState == AppState.ShowAward)
            {
                LoadCanWinUsers();
                 m_timer.Start();

                GoToState(AppState.Gaming);
            }
            else if(m_currentState == AppState.ShowWinner)
            {
                if (m_currentAward == null)
                {
                    MessageBox.Show("该奖品已抽完");
                    return;
                }
                if( m_currentAward.PlanQty > 0)
                {
                    LoadCanWinUsers();
                    m_timer.Start();
                    GoToState(AppState.Gaming);
                }
                else
                {
                    ShowAward(m_currentAward.AwardSeq);
                }
            }
            else if (m_currentState == AppState.Gaming)
            {
                if(m_candidateIds == null)
                {
                    MessageBox.Show("debug get GetCanWinUsers 未返回");
                    return;
                }
                
                if(m_candidateIds.Count() == 0)
                {
                    MessageBox.Show("所有用户抽过了");
                    return;
                }

                //阳光普照
                if (m_currentAward.AwardSeq == 5)
                {
                    Task.Factory.StartNew(() =>
                    {
                        foreach (var userId in m_candidateIds)
                        {
                            var userAwardResult = m_gameService.WinAwardByUser(m_currentAward.Id.ToString(), userId.ToString()).Result;
                            if (userAwardResult.Data == null)
                            {
                                log.Error("发送阳光普照失败 " + userId);
                            }
                        }
                    });
                    MessageBox.Show("阳光普照奖品包发送完毕");
                    return;
                }

                //todo:scanuser less than candidates
                UserActionData winner = m_scanUsers.FirstOrDefault(u => u.Id == m_candidateIds.RandomGet());
                if (winner == null) { 
                    return;
                }
                m_timer.Stop();

                SetWinner(winner, m_currentAward);
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

        private void SetWinner(UserActionData winner, AwardData award)
        {
            m_currentAward.PlanQty--;

            SetWinnerImage(winner);
            Task.Factory.StartNew(() => {
               var userAwardResult = m_gameService.WinAwardByUser(award.Id.ToString(), winner.Id.ToString()).Result;
                if (userAwardResult.Data == null)
                {
                    Console.WriteLine("WinAwardByUser " + userAwardResult.ErrMessage);
                }
                else
                {
                    Console.WriteLine("WinAwardByUser" + userAwardResult.Data.Nickname + "win " + userAwardResult.Data.AwardID);
                }
            });
        }

        private void SetWinnerImage(UserInfoData winner)
        {
            winnerImage.Source = new BitmapImage(new Uri(winner.Headimgurl, UriKind.Absolute));
            
            winnerName.Text = winner.Nickname;
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
                CheckInQrcode q = ReadFromDisk();
                if(q != null)
                {
                    return new QrCodeData
                    {
                        QrCodeId = q.QrcodeId,
                        QrCodeUrl = q.FileName
                      
                    };
                }
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
                    var filename = "qrcode.jpg";
                    Console.WriteLine();
                    bool success = DownloadImage(qrCodeResult.Data.QrCodeUrl, AppDomain.CurrentDomain.BaseDirectory + filename);
                    if (!success)
                        continue;
                    qrCodeResult.Data.QrCodeUrl = filename;
                    SaveQrcode(new CheckInQrcode { FileName = filename, QrcodeId = qrCodeResult.Data.QrCodeId, ActivityId = Activity_ID});
                    return qrCodeResult.Data;
                    
                }
                return null;
            });

            //二维码图片 UI
            fetchQrcodeTask.ContinueWith((t) => 
            {
                QrCodeData qrcodeData = t.Result;
                if (qrcodeData == null)
                {
                    //MessageBox.Show("获取二维码失败");
                }
                qrcodeImge.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + qrcodeData.QrCodeUrl, UriKind.Absolute));
            }, TaskScheduler.FromCurrentSynchronizationContext());

            //询轮扫人用户
            fetchQrcodeTask.ContinueWith((t) => 
            {
                QrCodeData qrcodeData = t.Result;
                if (qrcodeData == null)
                {
                    MessageBox.Show("获取二维码失败");
                }
                m_longPulling = true;
                while(m_longPulling)
                {
                    Thread.Sleep(1000);
                    //var userActionResult = m_gameService.FindScanQrCodeUsersAsync(qrcodeData.QrCodeId).Result;
                    var userActionResult = m_gameService.GetUsersByActivityAndGame(10000).Result;
                    if (userActionResult == null)
                        continue;
#if DEBUG
                    Console.WriteLine("FindScanQrCodeUsersAsync " + userActionResult.ErrMessage);

#endif
                    if (userActionResult.Data == null)
                    {
                        continue;
                    }
                    var scanUsers = userActionResult.Data;

                    if (scanUsers.Count() != m_lastUserCount)
                    {
                        WebClient webClient = new WebClient();
                        var appRoot = AppDomain.CurrentDomain.BaseDirectory;
                        var headDir = appRoot + "head/";
                        if (!Directory.Exists(headDir)) Directory.CreateDirectory(headDir);
                        foreach (var u in scanUsers)
                        {
                            if (string.IsNullOrEmpty(u.Headimgurl))
                            {
                                u.Headimgurl = AppDomain.CurrentDomain.BaseDirectory + "nohead.png";
                                continue;
                            }
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
                            catch (Exception e)
                            {
                                log.Error(e);
                            }
                        }

                        m_lastUserCount = scanUsers.Where(s => s.IsSigned).Count();
                        Dispatcher.BeginInvoke((Action)(() => 
                        {
                            wall.ClearTiles();
                            m_scanUsers = scanUsers.Where(s => s.IsSigned).ToList();
                            usersCountText.Text = m_scanUsers.Count.ToString();

                            foreach (var usr in m_scanUsers)
                            {
                                wall.AddTile(usr.Id, usr.Headimgurl);
                            }
                        }));
                    }
                }

            });
        }

        private void LoadCanWinUsers()
        {
            m_candidateIds = null;
           var fetchCandicateTask = Task.Factory.StartNew<List<UserAwardData>>(() => 
           {
               int tryCount = 0;
               while (tryCount < 3)
               {
                   var userAwardsResult = m_gameService.GetCanWinUsers(m_currentAward.Id.ToString()).Result;
                   if (userAwardsResult.Data != null)
                       return userAwardsResult.Data;
               }
               return null;
           });

            fetchCandicateTask.ContinueWith((t) =>
            {
                List<UserAwardData> candicates = t.Result;
                if(candicates != null)
                {
                    m_candidateIds = candicates.Select(c => c.Id).ToList();
                }
                else
                {
                    m_candidateIds = null;
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private bool DownloadImage(string url, string dest)
        {
            WebClient webClient = new WebClient();
            try
            {
                var tmp = dest + ".downloading";
                webClient.DownloadFile(url, tmp);
                if(File.Exists(dest))
                {
                    File.Delete(dest);
                }
                File.Move(tmp, dest);
                return true;
            }
            catch (Exception e) { }
            return false;
        }

        public void ScanCheckIn(string url)
        {
            wall.AddTile(1,url);
            if (m_scanUsers == null)
                m_scanUsers = new List<UserActionData>();
            m_scanUsers.Add(new UserActionData {
                Headimgurl = url,
                Nickname = "Name " + url.GetHashCode(),
                Id = url.GetHashCode()
            });
            usersCountText.Text = m_scanUsers.Count.ToString();
        }      

        private void GoToState(AppState state)
        {
            qrcodePanel.Visibility = state == AppState.ScanQrcode ? Visibility.Visible : Visibility.Hidden;
            awardPanel.Visibility = state != AppState.ScanQrcode ? Visibility.Visible : Visibility.Hidden;
            winnerPanel.Visibility = state == AppState.ShowWinner || state == AppState.Gaming ? Visibility.Visible : Visibility.Hidden;
            m_currentState = state;
            if(m_currentState != AppState.ScanQrcode)
            {
                m_longPulling = false;
            }
        }

        private void SaveQrcode(CheckInQrcode q)
        {
            var appRoot = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
               File.WriteAllText(appRoot + "qrcode.json" ,JsonConvert.SerializeObject(q));
            }
            catch (Exception) { }
        }

        private CheckInQrcode ReadFromDisk()
        {
            var appRoot = AppDomain.CurrentDomain.BaseDirectory;
            try {

                CheckInQrcode q = JsonConvert.DeserializeObject<CheckInQrcode>(File.ReadAllText(appRoot + "qrcode.json"));
                if(q.ActivityId != Activity_ID)
                {
                    return null;
                }
                if(File.Exists(appRoot + q.FileName))
                {
                    return q;
                }
             }
            catch (Exception) { }
            return null;
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

    class CheckInQrcode
    {
        public string QrcodeId { get; set; }
        public string FileName { get; set; }
        public string ActivityId { get; set; }
    }

    public static class ListExtension
    {
        public static T RandomGet<T>(this IEnumerable<T> list)
        {
            if (list.Count() == 0)
                return default(T);
            Random rnd = new Random();
            return list.ElementAt(rnd.Next(list.Count()));
        }

    }

}
