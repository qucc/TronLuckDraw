using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LuckDraw
{
    public class QrcodeWallControl : Canvas
    {
        private const int Row = 5;
        private const int Column = 10;
        private const int Margin = 5;

        private List<QrcodeTile> m_tiles = new List<QrcodeTile>();
        private int[] m_tileIndexs = new int[Row * Column];

        public QrcodeWallControl()
        {
        }

        public void ClearTiles()
        {
            m_tiles.Clear();
        }

        public void AddTile(int userId, string qrcodeUrl)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(qrcodeUrl, UriKind.Absolute));
            m_tiles.Add(new QrcodeTile { UserId = userId, HeadImage = bitmap});
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (m_tiles.Count == 0)
                return;

            int startX = 0,
                startY = 0;
            int tileWidth = ((int)this.Width + Margin) / Column - Margin;
            int tileHeight = ((int)this.Height + Margin) / Row - Margin;

            List<int> unuse = Enumerable.Range(0, m_tileIndexs.Count()).ToList();
            int countPerTile = m_tileIndexs.Length / m_tiles.Count();
            Random rnd = new Random();
            for (var i = 0; i < m_tiles.Count(); i++)
            {
                for (var j = 0; j < countPerTile; j++)
                {
                    var index = rnd.Next(0, unuse.Count);
                    var unuseIndex = unuse[index];
                    unuse.RemoveAt(index);
                    m_tileIndexs[unuseIndex] = i;
                }
            }

            foreach (var tileIndex in m_tileIndexs)
            {
                dc.DrawImage(m_tiles[tileIndex].HeadImage, new Rect(startX, startY, tileWidth, tileHeight));
                startX = startX + tileWidth + Margin;
                if (startX >= this.Width)
                {
                    startX = 0;
                    startY = startY + tileHeight + Margin;
                }
            }
        }


    }


}
