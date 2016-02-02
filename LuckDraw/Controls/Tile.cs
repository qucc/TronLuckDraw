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

    public class Cubic
    {
        public TranslateTransform3D Tranlsate3D { get; set; }
        public AxisAngleRotation3D Rotation3D { get; set; }
        public DiffuseMaterial FrontMaterial { get; set; }
        public DiffuseMaterial BackMaterial { get; set; }
        public DiffuseMaterial TopMaterial { get; set; }
        public DiffuseMaterial BottomMaterial { get; set; }
        public DiffuseMaterial LeftMaterial { get; set; }
        public DiffuseMaterial RightMaterial { get; set; }
    }
}
