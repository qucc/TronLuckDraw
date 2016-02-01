using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace LuckDraw
{
    public class Qrcode
    {
        public BitmapImage HeadImage { get; set; }
        public int UserId { get; set; }
        public int AwardId { get; set; }
    }
}
