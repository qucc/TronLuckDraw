using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace LuckDraw
{
    public class Tile
    {
        public TranslateTransform3D Tranlsate3D { get; set; }
        public AxisAngleRotation3D Rotation3D { get; set; }
        public DiffuseMaterial Material { get; set; }
        public DiffuseMaterial BackMateral { get; set; }
    }
}
