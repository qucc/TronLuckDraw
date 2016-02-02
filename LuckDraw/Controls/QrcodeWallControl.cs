using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LuckDraw
{
    public class QrcodeWallControl : Viewport3D
    {
        private const int Row = 5;
        private const int Column = 10;

        private List<Qrcode> m_qrcodes = new List<Qrcode>();
        private int[] m_tileIndexs = new int[Row * Column];

        private PerspectiveCamera m_camera = null;
        private Model3DGroup m_world = null;
        private Model3DGroup m_tileModels = null;
        private TranslateTransform3D m_tilesTranslate3D = null;
        private MeshGeometry3D m_tileFrontMesh = null;
        private MeshGeometry3D m_tileBottomMesh = null;
        private MeshGeometry3D m_tileSideMesh = null;
        private Model3DGroup m_cubicModels = null;

        private Tile[] m_tiles = new Tile[Row * Column];
        private Cubic[] m_cubics = new Cubic[5];


        public QrcodeWallControl()
        {
            Init3DWorld();
            InitWallTiles();
            foreach (var f in Directory.GetFiles("head"))
            {
                m_qrcodes.Add(new Qrcode
                {
                    HeadImage = new BitmapImage(new Uri(f, UriKind.Relative))
                });
            }
            PaintFrontWall();
            PaintBackWall();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            
        }

        bool start = false;
        double friction = 0;
        double speed = 6;
        private double angle = 0;
        private DateTime lastFlipTime = DateTime.Now;
        private bool isFliping = false;
        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if(!isFliping && DateTime.Now.Subtract(lastFlipTime).TotalMilliseconds > 2000)
            {
                isFliping = true;
            }

            if(isFliping)
            {
                FlipTiles();
            }

            if (start)
            {
                speed = speed - friction;
                if (speed < 0)
                {
                    start = false;
                }
                angle = angle + speed;

                for (int i = 0; i < m_cubics.Length; i++)
                {
                    var cubic = m_cubics[i];
                    cubic.Rotation3D.Angle = angle;
                }
                
             
                 PaintCubic();
                

                if (angle == 360)
                {
                    angle = 0;
                }
            }
        }

        private void Init3DWorld()
        {
            ModelVisual3D rootVisual = new ModelVisual3D();
            m_world = new Model3DGroup();
            m_tileModels = new Model3DGroup();
            m_tilesTranslate3D = new TranslateTransform3D();
            m_tileModels.Transform = m_tilesTranslate3D;
            m_cubicModels = new Model3DGroup();

            m_camera = new PerspectiveCamera();
            m_camera.Position = new Point3D(0, 0, 5);
            m_camera.LookDirection = new Vector3D(0, 0, -1);
            m_camera.FieldOfView = 90;

            DirectionalLight directionLight = new DirectionalLight();
            directionLight.Direction = new Vector3D(0, 0, -1);
            directionLight.Color = Colors.Gainsboro;
            AmbientLight ambientLight = new AmbientLight();
           
            ambientLight.Color = Colors.Gainsboro;

            this.Camera = m_camera;
            this.Children.Add(rootVisual);
            rootVisual.Content = m_world;

            m_world.Children.Add(directionLight);
            m_world.Children.Add(ambientLight);
            m_world.Children.Add(m_tileModels);
            m_world.Children.Add(m_cubicModels);

            m_tileFrontMesh = new MeshGeometry3D();
            m_tileFrontMesh.Positions = Point3DCollection.Parse("0 0 0, 0 1 0,1 1 0,1 0 0");
            m_tileFrontMesh.TriangleIndices = Int32Collection.Parse("1 0 2, 2 0 3");
            m_tileFrontMesh.TextureCoordinates = PointCollection.Parse("1 1, 1 0, 0 0, 0 1");

            m_tileBottomMesh = new MeshGeometry3D();
            m_tileBottomMesh.Positions = Point3DCollection.Parse("0 0 0, 1 0 0, 1 0 1,0 0 1");
            m_tileBottomMesh.TriangleIndices = Int32Collection.Parse("0 1 2, 0 2 3");
            m_tileBottomMesh.TextureCoordinates = PointCollection.Parse("1 1, 1 0, 0 0, 0 1");

            m_tileSideMesh = new MeshGeometry3D();
            m_tileSideMesh.Positions = Point3DCollection.Parse("0 0 0,0 0 1,0 1 1,0 1 0");
            m_tileSideMesh.TriangleIndices = Int32Collection.Parse("0 1 2, 0 2 3");
            m_tileSideMesh.TextureCoordinates = PointCollection.Parse("1 1, 1 0, 0 0, 0 1");

        }
        
        public void ClearTiles()
        {
            m_qrcodes.Clear();
        }

        public void AddQrcode(int userId, string qrcodeUrl)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(qrcodeUrl, UriKind.Absolute));
            m_qrcodes.Add(new Qrcode { UserId = userId, HeadImage = bitmap});

        }

        private void InitWallTiles()
        {
            for (int i = 0; i < Row * Column; i++)
            {
                m_tiles[i] = new Tile
                {
                    Tranlsate3D = new TranslateTransform3D(i % 10 - 5, i / 10 - 2.5, 0),
                    Rotation3D  = new AxisAngleRotation3D(new Vector3D(0,1,0),0),
                    Material = new DiffuseMaterial(),
                    BackMateral = new DiffuseMaterial()
                };
            }

            m_tileModels.Children.Clear();
            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileFrontMesh;
                Transform3DGroup trans = new Transform3DGroup();
                trans.Children.Add(new RotateTransform3D(tile.Rotation3D,  new Point3D(0.5, 0.5, 0)));
                trans.Children.Add(tile.Tranlsate3D);
                tileModel.Transform = trans;
                tileModel.Material = tile.Material;
                tileModel.BackMaterial = tile.BackMateral;
                m_tileModels.Children.Add(tileModel);
            }
        }

        private void PaintFrontWall()
        {
            Random rnd = new Random();
            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                Qrcode qrcode = m_qrcodes[rnd.Next(50)];
                tile.Material.Brush = new ImageBrush(qrcode.HeadImage);
                
            }
        }

        private void PaintBackWall()
        {
            Random rnd = new Random();
            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                Qrcode back = m_qrcodes[rnd.Next(50)];
                tile.BackMateral.Brush = new ImageBrush(back.HeadImage);
            }
        }

        private void FlipTiles()
        {
            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                tile.Rotation3D.Angle += 1;
                if(tile.Rotation3D.Angle == 360)
                {
                    tile.Rotation3D.Angle = 0;
                    isFliping = false;
                    lastFlipTime = DateTime.Now;
                    PaintBackWall();
                }
                if(tile.Rotation3D.Angle == 180)
                {
                    isFliping = false;
                    lastFlipTime = DateTime.Now;
                    PaintFrontWall();
                }
            }
        }

        private void ReloadCubic()
        {
            m_cubicModels.Children.Clear();
            double offset = (m_cubics.Length * 1.5 - 0.5) / 2;
            for (int i = 0; i < m_cubics.Length; i++)
            {
                m_cubics[i] = new Cubic
                {
                    Tranlsate3D = new TranslateTransform3D(i * 1.5 - offset, 0, 1),
                    Rotation3D  = new AxisAngleRotation3D(new Vector3D(1, 0, 1), 0),
                    BackMaterial = new DiffuseMaterial(),
                    BottomMaterial = new DiffuseMaterial(),
                    FrontMaterial = new DiffuseMaterial(new SolidColorBrush(Colors.Black)),
                    LeftMaterial = new DiffuseMaterial(),
                    RightMaterial = new DiffuseMaterial(),
                    TopMaterial = new DiffuseMaterial()
                };
            }

            for (int i = 0; i < m_cubics.Length; i++)
            {
                var cubic = m_cubics[i];
                var cubicModel3D = BuildCubic(cubic);
                m_cubicModels.Children.Add(cubicModel3D);
            }
        }

        private Model3DGroup BuildCubic(Cubic cubic)
        {
            Model3DGroup cubicModel3D = new Model3DGroup();
            int row = 1;
            double offset = row / 2.0;
            //front
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileFrontMesh;
                tileModel.Material = cubic.FrontMaterial;
                tileModel.BackMaterial = cubic.FrontMaterial;
                tileModel.Transform = new TranslateTransform3D(i % row - offset, i / row - offset, offset);
                cubicModel3D.Children.Add(tileModel);
            }
            //bottom
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileBottomMesh;
                tileModel.Material = cubic.BottomMaterial;
                tileModel.BackMaterial = cubic.BottomMaterial;
                tileModel.Transform = new TranslateTransform3D(i % row - offset, -offset, i / row - offset);
                cubicModel3D.Children.Add(tileModel);
            }
            ////left
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileSideMesh;
                tileModel.Material = cubic.LeftMaterial;
                tileModel.BackMaterial = cubic.LeftMaterial;
                tileModel.Transform = new TranslateTransform3D(-offset, i % row - offset, i / row - offset);
                cubicModel3D.Children.Add(tileModel);
            }
            //behind
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileFrontMesh;
                tileModel.Material = cubic.BackMaterial;
                tileModel.BackMaterial = cubic.BackMaterial;
                tileModel.Transform = new TranslateTransform3D(i % row - offset, i / row - offset, -offset);
                cubicModel3D.Children.Add(tileModel);
            }
            //top
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileBottomMesh;
                tileModel.Material = cubic.TopMaterial;
                tileModel.BackMaterial = cubic.TopMaterial;
                tileModel.Transform = new TranslateTransform3D(i % row - offset, offset, i / row - offset);
                cubicModel3D.Children.Add(tileModel);
            }
            //right
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileSideMesh;
                tileModel.Material = cubic.RightMaterial;
                tileModel.BackMaterial = cubic.RightMaterial;
                tileModel.Transform = new TranslateTransform3D(offset, i % row - offset, i / row - offset);
                cubicModel3D.Children.Add(tileModel);
            }
            RotateTransform3D rotate = new RotateTransform3D(cubic.Rotation3D, new Point3D(0, 0, 0));
            Transform3DGroup transGroup = new Transform3DGroup();
            transGroup.Children.Add(rotate);
            transGroup.Children.Add(cubic.Tranlsate3D);
            cubicModel3D.Transform = transGroup;
            return cubicModel3D;
        }

        private void PaintCubic()
        {
            var rnd = new Random();
            
            for (int i = 0; i < m_cubics.Length; i++)
            {
                var cubic = m_cubics[i];
                cubic.BackMaterial.Brush = new ImageBrush(m_qrcodes[rnd.Next(m_qrcodes.Count)].HeadImage);
                cubic.BottomMaterial.Brush = new ImageBrush(m_qrcodes[rnd.Next(m_qrcodes.Count)].HeadImage);
                cubic.FrontMaterial.Brush = new ImageBrush(m_qrcodes[rnd.Next(m_qrcodes.Count)].HeadImage);
                cubic.LeftMaterial.Brush = new ImageBrush(m_qrcodes[rnd.Next(m_qrcodes.Count)].HeadImage);
                cubic.RightMaterial.Brush = new ImageBrush(m_qrcodes[rnd.Next(m_qrcodes.Count)].HeadImage);
                cubic.TopMaterial.Brush = new ImageBrush(m_qrcodes[rnd.Next(m_qrcodes.Count)].HeadImage);
            }
        }
        
        public void Roll()
        {
            ReloadCubic();
            if (!start)
            {
                start = true;
                friction = 0;
            }
            else
            {
                friction = speed * speed / (360 * 2 - angle) / 2;
            }
        }

    }


}
