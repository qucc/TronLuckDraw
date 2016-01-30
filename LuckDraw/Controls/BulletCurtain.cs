using SensngGame.ClientSDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LuckDraw
{
    public class BulletCurtain : Image
    {
        private delegate void NewBulletDelegate(List<Bullet> bullets);
        private DrawingGroup m_bulletCurtain;
        private Rect m_curtainRect;
        private DispatcherTimer m_bulletTimer = new DispatcherTimer();
        private List<Bullet> m_bullets = new List<Bullet>();
        private int m_lastBulletId = -1;
        private bool m_running = true;
        private Brush[] m_brushes = new Brush[] {Brushes.LightSeaGreen, Brushes.Pink,Brushes.Yellow};

        private GameServiceClient m_gameServiceClient;
        public BulletCurtain()
        {
            InitBulletCurtain();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (m_running)
            {
                bulletTimer_Tick(null, null);
            }
        }

        public void SetGameServiceClient(GameServiceClient client)
        {
            m_gameServiceClient = client;
            Start();
        }

        private void InitBulletCurtain()
        {
            m_bulletCurtain = new DrawingGroup();
            DrawingImage drawingImage = new DrawingImage(m_bulletCurtain);
            Source = drawingImage;
            m_curtainRect = new Rect(0, 0, 1280, 720);
            m_bulletCurtain.ClipGeometry = new RectangleGeometry(m_curtainRect);

            //m_bulletTimer.Interval = TimeSpan.FromMilliseconds(100);
            //m_bulletTimer.Tick += bulletTimer_Tick;
            //m_bulletTimer.Start();
        }



        int id = 1;

        private void PullingBulletsFromServer()
        {
            m_running = true;
            Random rnd = new Random();

            Task.Factory.StartNew(() =>
            {
                while (m_running)
                {

                    var chatMessageResult = m_gameServiceClient.GetChartMessage(5).Result;
                    if(chatMessageResult.Data != null)
                    {
                        List<Bullet> bullets = new List<Bullet>();
                        foreach(var chat in chatMessageResult.Data)
                        {
                            bullets.Add(new Bullet
                            {
                                Id = chat.Id,
                                Text = chat.Message,
                                X = (int)m_curtainRect.Width + rnd.Next(50,100),
                                Y = rnd.Next(10, 600),
                                Speed = rnd.Next(2, 5),
                                BrushIndex = rnd.Next(m_brushes.Length)
                            });
                        }
                        Dispatcher.BeginInvoke(new NewBulletDelegate(NewBullets), bullets);
                    }

#if DEBUG
                    Console.WriteLine("Loading Bullets...");
#endif

                    Thread.Sleep(1000);
                }
            });
        }

        private void NewBullets(List<Bullet> bullets)
        {
            var newBullest = bullets.Except(m_bullets, new BulletComparer());
            if(m_lastBulletId != -1)
            {
                newBullest = newBullest.Where(b => b.Id > m_lastBulletId);
            }
            if (newBullest.Count() > 0 && m_brushes.Length < 25)
            {
                m_bullets.AddRange(newBullest.OrderBy(b => b.Id));
                m_lastBulletId = Math.Max(m_lastBulletId, m_bullets.Max(b => b.Id));
            }

        }

        public void Toggle()
        {
            if (m_running)
                Stop();
            else
                Start();
        }

        public void Start()
        {
            m_bulletTimer.Start();
            PullingBulletsFromServer();

        }

        public void Stop()
        {
            m_bulletTimer.Stop();
            m_running = false;
            ClearBullets();
        }

        private void ClearBullets()
        {
            using (var dc = m_bulletCurtain.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, m_curtainRect);
            }
        }

        private void bulletTimer_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < m_bullets.Count; i++)
            {
                var bullet = m_bullets[i];
                bullet.X -= bullet.Speed;
                if (bullet.X < -100)
                {
                    m_bullets.RemoveAt(i);
                }
            }

            using (var dc = m_bulletCurtain.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, m_curtainRect);
                foreach (var bullet in m_bullets)
                {
                    var formattedText= new FormattedText(bullet.Text,
                                        CultureInfo.CurrentCulture,
                                        FlowDirection.LeftToRight,
                                        new Typeface("Arial"), 30,
                                        Brushes.White);
                    var margin = 10;
                    dc.PushOpacity(0.6);
                    dc.DrawRoundedRectangle(m_brushes[bullet.BrushIndex], null, new Rect(bullet.X - margin, bullet.Y - margin, formattedText.Width + 2 * margin, formattedText.Height + 2 * margin), 10, 10);
                    dc.Pop();
                    dc.DrawText(formattedText, new Point(bullet.X, bullet.Y));
               
                    
                }
            }

        }

        class Bullet
        {
            public int Id { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Speed { get; set; }
            public string Text { get; set; }
            public int BrushIndex { get; set; }

        }

        class BulletComparer : IEqualityComparer<Bullet>
        {
            public bool Equals(Bullet x, Bullet y)
            {
                return x.Id == y.Id;
            }

            public int GetHashCode(Bullet obj)
            {
                return obj.Id;
            }
        }
    }
}
