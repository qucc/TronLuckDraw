﻿using Newtonsoft.Json;
using SensngGame.ClientSDK;
using SensngGame.ClientSDK.Contract;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private bool m_isEnableWhiteList;

        private List<UserActionData> m_scanUsers = null;
        private List<AwardData> m_awards = null;
        
        private AppState m_currentState = AppState.ScanQrcode;
        private AwardData m_currentAward = null;
        private List<int> m_candidateIds = null;
        private Random m_rnd = new Random();
        private readonly string Activity_ID = ConfigurationManager.AppSettings["ActivityId"].ToString();
        private readonly string Weixin_ID = ConfigurationManager.AppSettings["WeixinId"].ToString();

        public LuckDrawControl()
        {
            InitializeComponent();
            m_gameService = new GameServiceClient("j;lajdf;jaiuefjf", Weixin_ID, "19", Activity_ID);
        
            bulletCurtain.SetGameServiceClient(m_gameService);
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
            LoadQrcode();
            LoadAwardList();
            LoadActivityInfo();
                LoadWhiteListInfo();
            }
    
        }

        private void LoadWhiteListInfo()
        {
           var fetchWhiteListTask = Task.Factory.StartNew<List<WhiteUserData>>(() => 
            {
                int tryCount = 0;
                while (tryCount < 3)
                {
                    var whitelistResult = m_gameService.GetActivityWhiteListUsers().Result;
                    if(whitelistResult.Data != null)
                    {
                        return whitelistResult.Data;
                    }
                    tryCount++;
                }
                return null;
            });

            fetchWhiteListTask.ContinueWith((t) => 
            {
                List<WhiteUserData> list = t.Result;
                if(list ==  null)
                {
                    MessageBox.Show("加载白名单失败");
                    return;
                }
                totalCountText.Text = list.Count.ToString();
                if (!m_isEnableWhiteList)
                {
                    totalCountTextSlash.Text = "";
                    totalCountText.Text = "";
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void LoadActivityInfo()
        {
           var fetchActivityTask = Task.Factory.StartNew<ActivityData>(() => 
            {
                int tryCount = 0;
                while (tryCount < 3)
                {
                    var activityResult = m_gameService.GetActivityInfo().Result;
                    if (activityResult.Data != null)
                        return activityResult.Data;
                    else
                        log.Error(activityResult.ErrMessage);
                }
                return null;
            });

            fetchActivityTask.ContinueWith((t) =>
            {
                ActivityData activityData = t.Result;
                if(activityData == null)
                {
                    MessageBox.Show("获取不到活动信息");
                    return;
                }
                if(DateTime.Today < activityData.OpenDate)
                {
                    MessageBox.Show("活动未开始");
                }
                if(DateTime.Today > activityData.EndDate)
                {
                    MessageBox.Show("活动已结束");
                }
                titleTxt.Text = activityData.Name + "微信签到墙";
                m_isEnableWhiteList = activityData.IsEnableWhiteUser;
                if(!m_isEnableWhiteList)
                {
                    totalCountTextSlash.Text = "";
                    totalCountText.Text = "";
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
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
            
            if(!string.IsNullOrEmpty(m_currentAward.AwardImagePath))
                awardImage.Source = new BitmapImage(new Uri(m_currentAward.AwardImagePath));
            awardText.Text = m_currentAward.Name;
            awardNameText.Text = m_currentAward.AwardProduct;
            awardCountText.Text = m_currentAward.ActualQty + "/" + m_currentAward.PlanQty;
            wall.Reset();
            GoToState(AppState.ShowAward);
        }

        public void Begin()
        {
            if(m_currentState == AppState.ShowAward)
            {
                LoadCanWinUsers();
                wall.Roll();

                GoToState(AppState.Gaming);
            }
            else if(m_currentState == AppState.ShowWinner)
            {
                if (m_currentAward == null)
                {
                    MessageBox.Show("该奖品已抽完");
                    return;
                }
                if( m_currentAward.PlanQty > m_currentAward.ActualQty)
                {
                    LoadCanWinUsers();
                    wall.Roll();
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
                wall.Stop();

                var cubicCount = wall.CubicCount;
                var winnerIds = m_candidateIds.RandomGet(cubicCount);
                var winnerCount = winnerIds.Count();
                var awardCount = m_currentAward.PlanQty - m_currentAward.ActualQty;
                
                for (int i = 0; i < cubicCount; i++)
                {
                    if (i < Math.Min(awardCount, winnerCount))
                    {
                        UserActionData winner = m_scanUsers.FirstOrDefault(u => u.Id == winnerIds.ElementAt(i));
                        if (winner == null)
                        {
                            MessageBox.Show("打不到用户");
                            continue;
                        }
                        SetWinner(i, winner, m_currentAward);
                    }
                    else
                    {
                        wall.PaintWinner(i, new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "sun.jpg", UriKind.Absolute)), "");
                    }
                }
                GoToState(AppState.ShowWinner);
            }
        }


        private void SetWinner(int index, UserActionData winner, AwardData award)
        {
            wall.PaintWinner(index, new BitmapImage(new Uri(winner.Headimgurl, UriKind.Absolute)), winner.Nickname);
            Task.Factory.StartNew<bool>(() =>
            {
                var userAwardResult = m_gameService.WinAwardByUser(award.Id.ToString(), winner.Id.ToString()).Result;
                if (userAwardResult.Data != null)
                {
                    Console.WriteLine("WinAwardByUser" + userAwardResult.Data.Nickname + "win " + userAwardResult.Data.AwardID);
                    return true;
                }
                Console.WriteLine("WinAwardByUser " + userAwardResult.ErrMessage);
                return false;
            })
            .ContinueWith((t) => {
                bool success = t.Result;
                if(success)
                {
                    m_currentAward.ActualQty++;
                    awardCountText.Text = m_currentAward.ActualQty + "/" + m_currentAward.PlanQty;
                }
                else
                {
                    wall.PaintWinner(index, new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "sun.jpg", UriKind.Absolute)), "");
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
                    var filename = "qrcode.jpg";
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
                    var userActionResult = m_gameService.GetUsersByActivityAndGame(1000).Result;
                    if (userActionResult == null)
                        continue;
#if DEBUG
                    Console.WriteLine("FindScanQrCodeUsersAsync " + userActionResult.ErrMessage);

#endif
                    if (userActionResult.Data == null)
                    {
                        continue;
                    }
                    var gameUsers = userActionResult.Data;

                    if (gameUsers.Where(s => s.IsSigned).Count() != m_lastUserCount)
                    {
                        WebClient webClient = new WebClient();
                        var appRoot = AppDomain.CurrentDomain.BaseDirectory;
                        var headDir = appRoot + "head/";
                        if (!Directory.Exists(headDir)) Directory.CreateDirectory(headDir);
                        foreach (var u in gameUsers)
                        {
                            if (string.IsNullOrEmpty(u.Headimgurl))
                            {
                                u.Headimgurl = appRoot + "nohead.png";
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

                        m_lastUserCount = gameUsers.Where(s => s.IsSigned).Count();
                        Dispatcher.BeginInvoke((Action)(() => 
                        {
                            wall.ClearTiles();
                            m_scanUsers = gameUsers.Where(s => s.IsSigned).ToList();
                            usersCountText.Text = m_scanUsers.Count.ToString();

                            foreach (var usr in m_scanUsers)
                            {
                                wall.AddQrcode(usr.Id, usr.Headimgurl);
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
            wall.AddQrcode(1,url);
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

        public void AddLuckOne()
        {
            if(m_currentAward != null)
            {
                if(m_currentAward.PlanQty - m_currentAward.ActualQty > wall.CubicCount)
                {
                    wall.AddCubic();
                }
            }
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

        public static IEnumerable<T> RandomGet<T>(this IEnumerable<T> list, int count)
        {
            if (list.Count() == 0)
                yield return default(T);
            Random rnd = new Random();
            var indexs = Enumerable.Range(0, list.Count()).ToList();
            var resultCount = Math.Min(list.Count(), count);
            for (int i = 0; i < resultCount; i++)
            {
                var indexIndex = rnd.Next(indexs.Count);
                var listIndex = indexs[indexIndex];
                indexs.RemoveAt(indexIndex);
                yield return list.ElementAt(listIndex);
            }

    }
    }

}
