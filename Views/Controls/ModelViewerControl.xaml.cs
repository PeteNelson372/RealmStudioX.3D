using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.SharpDX.Utilities;
using HelixToolkit.Wpf.SharpDX;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;

namespace RealmStudioX._3D.Views.Controls
{
    /// <summary>
    /// Interaction logic for ModelViewerControl.xaml
    /// </summary>
    public partial class ModelViewerControl : UserControl
    {
        private SceneNodeGroupModel3D? _modelGroup;
        private readonly TextureModelRepository _textureRepository = new();

        public ModelViewerControl()
        {
            InitializeComponent();

            Viewport.EffectsManager = new DefaultEffectsManager();

            CreateCamera();
            //CreateTestCube();
        }

        public void ResetCamera()
        {
            Viewport.Reset();
            Viewport.Camera?.Reset();

            if (Viewport.Camera is PerspectiveCamera perspectiveCamera)
            {
                perspectiveCamera.Position = new Point3D(5, 5, 5);
                perspectiveCamera.LookDirection = new Vector3D(-5, -5, -5);
                perspectiveCamera.UpDirection = new Vector3D(0, 1, 0);
                perspectiveCamera.FieldOfView = 45;
            };

        }

        private void ModelViewerControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Viewport.ActualWidth > 0 &&
                Viewport.ActualHeight > 0)
            {
                Viewport.ResizeAndArrange(
                    (int)Viewport.ActualWidth,
                    (int)Viewport.ActualHeight);
            }
        }

        private void CreateCamera()
        {
            var camera = new PerspectiveCamera
            {
                Position = new Point3D(5, 5, 5),
                LookDirection = new Vector3D(-5, -5, -5),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45
            };

            Viewport.Camera = camera;
            Viewport.DefaultCamera = camera;
        }

        public void LoadModel(string fileName)
        {
            using Importer importer = new();

            HelixToolkitScene? scene = importer.Load(fileName);

            if (scene == null || scene.Root == null)
                return;

            string directory = Path.GetDirectoryName(fileName)
                ?? string.Empty;

            string textureFileName =
                Path.Combine(directory, "plane_diffuse.png");

            TextureModel? texture =
                _textureRepository.Create(textureFileName);

            if (texture == null)
                return;

            foreach (SceneNode node in scene.Root.Traverse())
            {
                if (node is MeshNode meshNode)
                {
                    meshNode.Material = new DiffuseMaterialCore
                    {
                        DiffuseColor = Color4.White,
                        DiffuseMap = texture
                    };
                }
            }

            Viewport.Items.Clear();

            _modelGroup = new SceneNodeGroupModel3D();

            _modelGroup.AddNode(scene.Root);

            Viewport.Items.Add(_modelGroup);

            Viewport.ZoomExtents();
        }

        private void CreateTestCube()
        {
            var builder = new MeshBuilder();

            builder.AddBox(
                new Vector3(0, 0, 0),
                2,
                2,
                2);

            var mesh = builder.ToMesh().ToMeshGeometry3D();

            var model = new MeshGeometryModel3D
            {
                Geometry = mesh,
                Material = PhongMaterials.Green
            };

            Viewport.Items.Add(model);
        }
    }
}
