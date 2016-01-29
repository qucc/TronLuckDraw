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
        private object lockobject = new object();
        private bool m_running = true;
        private Brush[] m_brushes = new Brush[] { Brushes.White, Brushes.White, Brushes.White, Brushes.Green, Brushes.Pink,Brushes.Yellow};

        public BulletCurtain()
        {
            InitBulletCurtain();
            Start();
        }

        private void InitBulletCurtain()
        {
            m_bulletCurtain = new DrawingGroup();
            DrawingImage drawingImage = new DrawingImage(m_bulletCurtain);
            Source = drawingImage;
            m_curtainRect = new Rect(0, 0, 1280, 720);
            m_bulletCurtain.ClipGeometry = new RectangleGeometry(m_curtainRect);

            m_bulletTimer.Interval = TimeSpan.FromMilliseconds(100);
            m_bulletTimer.Tick += bulletTimer_Tick;
            m_bulletTimer.Start();
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
                    for (int i = 0; i < 1; i++)
                    {
                        var list = new List<Bullet>();
                        for (int j = 0; j < 1; j++)
                        {
                            list.Add(new Bullet
                            {
                                Id = id,
                                Text = "文本" + rnd.Next(10000, 90000).ToString(),
                                X = -300,
                                Y = rnd.Next(10, 600),
                                Speed = rnd.Next(10, 20),
                                BrushIndex = rnd.Next(m_brushes.Length)
                            });
                            id += 1;
                        }
                        Dispatcher.BeginInvoke(new NewBulletDelegate(NewBullets), list);

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
            m_bullets.AddRange(newBullest);

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
                bullet.X += bullet.Speed;
                if (bullet.X > m_curtainRect.Width)
                {
                    m_bullets.RemoveAt(i);
                }
            }

            using (var dc = m_bulletCurtain.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, m_curtainRect);
                foreach (var bullet in m_bullets)
                {
                    dc.DrawText(new FormattedText(bullet.Text,
                                    CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new Typeface("Arial"), 30,
                                    m_brushes[bullet.BrushIndex]),
                                    new Point(bullet.X, bullet.Y));
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
                return obj.GetHashCode();
            }
        }
    }
}
