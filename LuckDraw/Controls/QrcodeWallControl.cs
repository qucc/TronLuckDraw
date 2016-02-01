using System;
using System.Collections.Generic;
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
        private const int Margin = 5;

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
        private TranslateTransform3D m_cubicTranslate3D = null;
        private RotateTransform3D m_cubicRotate3D = null;
        private DispatcherTimer m_timer = new DispatcherTimer();

        private Tile[] m_tiles = new Tile[Row * Column];


        public QrcodeWallControl()
        {
            Init3DWorld();
            InitWallTiles();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        bool start = false;
        double friction = 0;
        double speed = 6;
        private double angle = 0;
        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (!start)
                return;
            speed = speed - friction;
            if (speed < 0)
            {
                start = false;
            }
            angle = angle + speed;
     
            (m_cubicRotate3D.Rotation as AxisAngleRotation3D).Angle = angle;

            if (angle == 360)
            {
                angle = 0;

            }
            if(angle % 12 == 0)
             AddImage();
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

           // ReloadCubic();
        }
        
        public void ClearTiles()
        {
            m_qrcodes.Clear();
        }

        public void AddQrcode(int userId, string qrcodeUrl)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(qrcodeUrl, UriKind.Absolute));
            m_qrcodes.Add(new Qrcode { UserId = userId, HeadImage = bitmap});
            RepaintWall();
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

        private void RepaintWall()
        {
            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                Qrcode qrcode = null;
                if (i < m_qrcodes.Count)
                    qrcode = m_qrcodes[i];
                if (qrcode != null)
                {
                    tile.Material.Brush = new ImageBrush(qrcode.HeadImage);
                }
            }
        }

        private void FlipTiles()
        {
            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                
            }
        }

        private void ReloadCubic()
        {
            m_cubicModels.Children.Clear();
      
            int row = 3;
            double offset = row / 2.0;
            //front
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileFrontMesh;
                tileModel.Material = new DiffuseMaterial(Brushes.Tomato);
                tileModel.BackMaterial = new DiffuseMaterial(Brushes.Tomato);
                tileModel.Transform = new TranslateTransform3D(i% row - offset, i/ row - offset, offset);
                m_cubicModels.Children.Add(tileModel);
            }
            //bottom
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileBottomMesh;
                tileModel.Material = new DiffuseMaterial(Brushes.Brown);
                tileModel.BackMaterial = new DiffuseMaterial(Brushes.Brown);
                tileModel.Transform = new TranslateTransform3D(i % row - offset, -offset, i / row - offset);
                m_cubicModels.Children.Add(tileModel);
            }
            ////left
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileSideMesh;
                tileModel.Material = new DiffuseMaterial(Brushes.Red);
                tileModel.BackMaterial = new DiffuseMaterial(Brushes.Red);
                tileModel.Transform = new TranslateTransform3D(-offset, i % row - offset, i / row - offset);
                m_cubicModels.Children.Add(tileModel);
            }
            //behind
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileFrontMesh;
                tileModel.Material = new DiffuseMaterial(Brushes.Yellow);
                tileModel.BackMaterial = new DiffuseMaterial(Brushes.Yellow);
                tileModel.Transform = new TranslateTransform3D(i % row - offset, i / row - offset, -offset);
                m_cubicModels.Children.Add(tileModel);
            }
            //top
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileBottomMesh;
                tileModel.Material = new DiffuseMaterial(Brushes.BurlyWood);
                tileModel.BackMaterial = new DiffuseMaterial(Brushes.BurlyWood);
                tileModel.Transform = new TranslateTransform3D(i % row - offset, offset, i / row - offset);
                m_cubicModels.Children.Add(tileModel);
            }
            //right
            for (int i = 0; i < row * row; i++)
            {
                GeometryModel3D tileModel = new GeometryModel3D();
                tileModel.Geometry = m_tileSideMesh;
                tileModel.Material = new DiffuseMaterial(Brushes.Cyan);
                tileModel.BackMaterial = new DiffuseMaterial(Brushes.Cyan);
                tileModel.Transform = new TranslateTransform3D(offset, i % row - offset, i / row - offset);
                m_cubicModels.Children.Add(tileModel);
            }
            m_cubicRotate3D = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 1), 0), new Point3D(0, 0, 0));
            m_cubicTranslate3D = new TranslateTransform3D(0,0,0);
            Transform3DGroup transGroup = new Transform3DGroup();
            transGroup.Children.Add(m_cubicRotate3D);
            transGroup.Children.Add(m_cubicTranslate3D);
            m_cubicModels.Transform = transGroup;

        }
        
        public void Roll()
        {
           
            ReloadCubic();
           
            //M_timer_Tick(null, null);     
            //m_timer.Start();
            if (!start)
            {
                start = true;
                friction = 0;

                var rotate = m_cubicRotate3D.Rotation as AxisAngleRotation3D;
                rotate.Angle = angle;

                if (angle == 360)
                {
                    angle = 0;

                }
            }
            else
            {

                friction = speed * speed / (360 * 2 - angle) / 2;
            }



        }

        public void AddImage()
        {
            var rnd = new Random();
            int i = 0;
            foreach(var tileModel in m_cubicModels.Children)
            {
                var geometry3D = tileModel as GeometryModel3D;
                geometry3D.Material = new DiffuseMaterial(new ImageBrush(m_qrcodes[rnd.Next(50)].HeadImage));
                geometry3D.BackMaterial = new DiffuseMaterial(new ImageBrush(m_qrcodes[rnd.Next(50)].HeadImage));
               

                i++;
            }
        }

    }


}
