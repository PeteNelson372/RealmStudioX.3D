using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.SharpDX.Utilities;
using HelixToolkit.Wpf;
using HelixToolkit.Wpf.SharpDX;
using RealmStudioShapeRenderingLib;
using RealmStudioShapeRenderingLib.Logging;
using SharpDX.Direct3D11;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Color = System.Windows.Media.Color;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using UserControl = System.Windows.Controls.UserControl;

namespace RealmStudioX._3D.Views.Controls
{
    /// <summary>
    /// Interaction logic for ModelViewer3DXControl.xaml.
    /// This control uses the Helix Toolkit Viewport3DX
    /// to render.
    /// </summary>
    public partial class ModelViewer3DXControl : UserControl, I3DModelViewer
    {
        private HelixToolkitScene? _loadedScene;

        public bool IsModelLoaded => _loadedScene != null && _loadedScene?.Root != null;

        private SceneNodeGroupModel3D? _modelGroup;

        private LineGeometryModel3D? _floorGrid;
        private LineGeometryModel3D? _boundingBox;

        private Rect3D? _modelBounds;

        public Rect3D? ModelBounds
        {
            get { return _modelBounds; }
            set { _modelBounds = value; }
        }

        private readonly TextureModelRepository _textureRepository = new();

        private static readonly Vector3D KeyLightDirection = new(-1, -1, -1);

        private static readonly Color KeyLightColor = (Color)ColorConverter.ConvertFromString("#909090");

        private static readonly Vector3D FillLightDirection = new(1, 0.5, 1);

        private static readonly Color FillLightColor = (Color)ColorConverter.ConvertFromString("#707070");

        private static readonly Color DefaultAmbientLightColor = (Color)ColorConverter.ConvertFromString("#404040");

        private const double PerspectiveNearPlane = 0.1;
        private const double OrthographicNearPlane = -1_000_000.0;
        private const double CameraFarPlane = 1_000_000.0;

        public Viewport3DX Viewport3D => Viewport;

        public ModelViewer3DXControl()
        {
            InitializeComponent();

            Viewport.EffectsManager = new DefaultEffectsManager();

            CreateDefaultLighting();
            CreateCamera();
        }

        private void CreateDefaultLighting()
        {
            AmbientLight.Color = DefaultAmbientLightColor;

            KeyLight.Direction = KeyLightDirection;

            KeyLight.Color = KeyLightColor;

            FillLight.Direction = FillLightDirection;

            FillLight.Color = FillLightColor;
        }

        // -------------------------
        // Model Viewer API Methods
        // -------------------------

        public void LoadModel(string fileName)
        {
            if (_modelGroup != null)
            {
                Viewport.Items.Remove(_modelGroup);
                _modelGroup = null;
            }

            using Importer importer = new();

            HelixToolkitScene? scene;

            try
            {
                scene = importer.Load(fileName);
            }
            catch (Exception ex)
            {
                RealmStudioXLogger.Error(ex.Message);
                throw;
            }

            if (scene?.Root == null)
                throw new InvalidOperationException(
                    "The model file did not contain a renderable scene.");

            _loadedScene = scene;

            EnsureUsableMaterials(scene);

            foreach (SceneNode node in scene.Root.Traverse())
            {
                if (node is not MeshNode meshNode)
                    continue;

                if (meshNode.Geometry is MeshGeometry3D meshGeometry)
                {            
                    RecalculateSmoothNormals(meshGeometry);
                }
            }

            //DumpMeshInformation(scene);

            if (_floorGrid != null &&
                !Viewport.Items.Contains(_floorGrid))
            {
                Viewport.Items.Add(_floorGrid);
            }

            _modelGroup = new SceneNodeGroupModel3D();
            _modelGroup.AddNode(scene.Root);

            Viewport.Items.Add(_modelGroup);

            ModelBounds = CalculateModelBounds();

            FitModel(ModelBounds);
        }

        private static void EnsureUsableMaterials(HelixToolkitScene scene)
        {
            foreach (SceneNode node in scene.Root.Traverse())
            {
                if (node is not MeshNode meshNode)
                    continue;

                if (meshNode.Material == null)
                {
                    meshNode.Material = CreateDefaultMaterial();
                }
                else if (meshNode.Material is DiffuseMaterialCore diffuseMaterial)
                {
                    meshNode.Material = ConvertDiffuseMaterial(diffuseMaterial);
                }
                else if (meshNode.Material is PhongMaterialCore)
                {
                    // Already using the material technique we want.
                }
                else
                {
                    // Leave other material types alone for now.
                }              
            }
        }

        private static void RecalculateSmoothNormals(MeshGeometry3D mesh)
        {
            if (mesh.Positions == null || mesh.Normals == null || mesh.Indices == null)
            {
                return;
            }

            Vector3[] accumulatedNormals = new Vector3[mesh.Positions.Count];

            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                int i0 = mesh.Indices[i];
                int i1 = mesh.Indices[i + 1];
                int i2 = mesh.Indices[i + 2];

                Vector3 p0 = mesh.Positions[i0];
                Vector3 p1 = mesh.Positions[i1];
                Vector3 p2 = mesh.Positions[i2];

                Vector3 edge1 = p1 - p0;
                Vector3 edge2 = p2 - p0;

                Vector3 faceNormal =
                    Vector3.Cross(edge1, edge2);

                if (faceNormal.LengthSquared() < 1e-12f)
                    continue;

                faceNormal = Vector3.Normalize(faceNormal);

                accumulatedNormals[i0] += faceNormal;
                accumulatedNormals[i1] += faceNormal;
                accumulatedNormals[i2] += faceNormal;
            }

            var normals =
                new Vector3Collection(
                    accumulatedNormals.Length);

            for (int i = 0; i < accumulatedNormals.Length; i++)
            {
                Vector3 normal =
                    accumulatedNormals[i];

                if (normal.LengthSquared() > 1e-12f)
                {
                    normal = Vector3.Normalize(normal);
                }
                else
                {
                    normal = Vector3.UnitY;
                }

                normals.Add(normal);
            }

            mesh.Normals = normals;
        }


        private static PhongMaterialCore ConvertDiffuseMaterial(
            DiffuseMaterialCore source)
        {
            return new PhongMaterialCore
            {
                AmbientColor = new Color4(0.15f, 0.15f, 0.15f, 1),
                DiffuseColor = source.DiffuseColor,

                DiffuseMap = source.DiffuseMap,
                DiffuseMapFilePath = source.DiffuseMapFilePath,

                UVTransform = source.UVTransform,
                DiffuseMapSampler = source.DiffuseMapSampler,

                EnableFlatShading = source.EnableFlatShading,
                VertexColorBlendingFactor = source.VertexColorBlendingFactor,

                RenderDiffuseMap = source.RenderDiffuseMap,

                SpecularColor = new Color4(0.15f, 0.15f, 0.15f, 1),
                SpecularShininess = 10,                
            };
        }

        private static PhongMaterialCore CreateDefaultMaterial()
        {
            return new PhongMaterialCore
            {
                AmbientColor = new Color4(0.15f, 0.15f, 0.15f, 1),
                DiffuseColor = new Color4(0.75f, 0.75f, 0.75f, 1),
                SpecularColor = new Color4(0.15f, 0.15f, 0.15f, 1),
                SpecularShininess = 10,
                EnableFlatShading = true
            };
        }

        public void SetCameraProjection(CameraProjection projection)
        {
            if (Viewport.Camera == null)
                return;

            Point3D position =
                Viewport.Camera.Position;

            Vector3D lookDirection =
                Viewport.Camera.LookDirection;

            Vector3D upDirection =
                Viewport.Camera.UpDirection;

            switch (projection)
            {
                case CameraProjection.Perspective:
                    {
                        if (Viewport.Camera is PerspectiveCamera)
                            return;

                        Viewport.Camera = new PerspectiveCamera
                        {
                            Position = position,
                            LookDirection = lookDirection,
                            UpDirection = upDirection,

                            FieldOfView = 45,

                            NearPlaneDistance = PerspectiveNearPlane,
                            FarPlaneDistance = CameraFarPlane
                        };

                        break;
                    }

                case CameraProjection.Orthographic:
                    {
                        if (Viewport.Camera is OrthographicCamera)
                            return;

                        if (!ModelBounds.HasValue)
                            return;

                        Rect3D bounds =
                            ModelBounds.Value;

                        //
                        // Find the model center.
                        //

                        Point3D center = new(
                            bounds.X + bounds.SizeX / 2.0,
                            bounds.Y + bounds.SizeY / 2.0,
                            bounds.Z + bounds.SizeZ / 2.0);

                        //
                        // Preserve the current viewing direction.
                        //

                        Vector3D forward = lookDirection;

                        if (forward.LengthSquared < 1e-12)
                            return;

                        forward.Normalize();

                        //
                        // Give the orthographic camera a substantial
                        // distance from the model.
                        //
                        // The distance does not control orthographic
                        // zoom; Width does that. The distance simply
                        // gives the camera controller plenty of room
                        // to operate.
                        //

                        double modelSize =
                            Math.Sqrt(
                                bounds.SizeX * bounds.SizeX +
                                bounds.SizeY * bounds.SizeY +
                                bounds.SizeZ * bounds.SizeZ);

                        double cameraDistance =
                            Math.Max(modelSize * 10.0, 1000.0);

                        Point3D orthoPosition =
                            center - forward * cameraDistance;

                        Viewport.Camera =
                            new OrthographicCamera
                            {
                                Position = orthoPosition,

                                LookDirection =
                                    center - orthoPosition,

                                UpDirection = upDirection,

                                Width = modelSize * 1.2,

                                NearPlaneDistance = OrthographicNearPlane,
                                FarPlaneDistance = CameraFarPlane
                            };

                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(projection),
                        projection,
                        null);
            }

            //
            // Fit using the newly selected projection.
            //

            FitModel(_modelBounds);
        }

        public void SetUpDirection(ModelUpDirection upDirection)
        {
            if (Viewport.Camera == null)
                return;

            var up = upDirection switch
            {
                ModelUpDirection.XUp => new Vector3D(1, 0, 0),
                ModelUpDirection.YUp => new Vector3D(0, 1, 0),
                ModelUpDirection.ZUp => new Vector3D(0, 0, 1),
                _ => throw new ArgumentOutOfRangeException(
                                        nameof(upDirection),
                                        upDirection,
                                        null),
            };

            Vector3D forward = Viewport.Camera.LookDirection;

            if (forward.LengthSquared < 1e-12)
                return;

            forward.Normalize();

            //
            // The up direction must not be parallel to the
            // viewing direction.
            //

            double alignment =
                Math.Abs(
                    Vector3D.DotProduct(
                        forward,
                        up));

            if (alignment > 0.999999)
                return;

            Viewport.Camera.UpDirection = up;

            FitModel(_modelBounds);
        }

        public void FitModel(Rect3D? bounds)
        {
            if (Viewport.Camera == null)
                return;

            if (bounds == null)
            {
                return;
            }

            double viewportWidth =
                Viewport.ActualWidth;

            double viewportHeight =
                Viewport.ActualHeight;

            if (viewportWidth <= 0.0 ||
                viewportHeight <= 0.0)
            {
                return;
            }

            //
            // Model center.
            //

            Point3D center = new(
                bounds.Value.X + bounds.Value.SizeX / 2.0,
                bounds.Value.Y + bounds.Value.SizeY / 2.0,
                bounds.Value.Z + bounds.Value.SizeZ / 2.0);

            //
            // Current camera orientation.
            //

            Vector3D forward = Viewport.Camera.LookDirection;

            if (forward.LengthSquared < 1e-12)
                return;

            forward.Normalize();

            Vector3D up = Viewport.Camera.UpDirection;

            if (up.LengthSquared < 1e-12)
                up = new Vector3D(0, 1, 0);
            else
                up.Normalize();

            //
            // Camera right vector.
            //

            Vector3D right =
                Vector3D.CrossProduct(forward, up);

            if (right.LengthSquared < 1e-12)
                return;

            right.Normalize();

            //
            // Bounding-box corners.
            //

            double minX = bounds.Value.X;
            double maxX = bounds.Value.X + bounds.Value.SizeX;

            double minY = bounds.Value.Y;
            double maxY = bounds.Value.Y + bounds.Value.SizeY;

            double minZ = bounds.Value.Z;
            double maxZ = bounds.Value.Z + bounds.Value.SizeZ;

            Point3D[] corners =
            [
                new(minX, minY, minZ),
                new(minX, minY, maxZ),
                new(minX, maxY, minZ),
                new(minX, maxY, maxZ),
                new(maxX, minY, minZ),
                new(maxX, minY, maxZ),
                new(maxX, maxY, minZ),
                new(maxX, maxY, maxZ)
            ];

            //
            // Project the bounding box into camera space.
            //

            double minHorizontal = double.MaxValue;
            double maxHorizontal = double.MinValue;

            double minVertical = double.MaxValue;
            double maxVertical = double.MinValue;

            double minDepth = double.MaxValue;
            double maxDepth = double.MinValue;

            foreach (Point3D corner in corners)
            {
                Vector3D offset =
                    corner - center;

                double horizontal =
                    Vector3D.DotProduct(
                        offset,
                        right);

                double vertical =
                    Vector3D.DotProduct(
                        offset,
                        up);

                double depth =
                    Vector3D.DotProduct(
                        offset,
                        forward);

                minHorizontal =
                    Math.Min(
                        minHorizontal,
                        horizontal);

                maxHorizontal =
                    Math.Max(
                        maxHorizontal,
                        horizontal);

                minVertical =
                    Math.Min(
                        minVertical,
                        vertical);

                maxVertical =
                    Math.Max(
                        maxVertical,
                        vertical);

                minDepth =
                    Math.Min(
                        minDepth,
                        depth);

                maxDepth =
                    Math.Max(
                        maxDepth,
                        depth);
            }

            double projectedWidth =
                maxHorizontal - minHorizontal;

            double projectedHeight =
                maxVertical - minVertical;

            double depthExtent =
                maxDepth - minDepth;

            if (projectedWidth <= 0.0)
                projectedWidth = 1.0;

            if (projectedHeight <= 0.0)
                projectedHeight = 1.0;

            if (depthExtent < 0.0)
                depthExtent = 0.0;

            //
            // Leave some breathing room around the model.
            //

            const double fitMargin = 1.20;

            //
            // ========================================================
            // Perspective camera
            // ========================================================
            //

            if (Viewport.Camera is PerspectiveCamera perspective)
            {
                double verticalFov =
                    perspective.FieldOfView *
                    Math.PI / 180.0;

                if (verticalFov <= 0.0 ||
                    verticalFov >= Math.PI)
                {
                    return;
                }

                double aspect =
                    viewportWidth /
                    viewportHeight;

                double horizontalFov =
                    2.0 *
                    Math.Atan(
                        Math.Tan(verticalFov / 2.0) *
                        aspect);

                //
                // Required distance for horizontal and vertical
                // extents.
                //

                double horizontalDistance =
                    (projectedWidth / 2.0) /
                    Math.Tan(horizontalFov / 2.0);

                double verticalDistance =
                    (projectedHeight / 2.0) /
                    Math.Tan(verticalFov / 2.0);

                double distance =
                    Math.Max(
                        horizontalDistance,
                        verticalDistance);

                //
                // Account for model depth.
                //

                distance +=
                    depthExtent / 2.0;

                distance *=
                    fitMargin;

                //
                // Position the camera along its current viewing
                // direction.
                //

                perspective.Position =
                    center -
                    forward * distance;

                perspective.LookDirection =
                    center -
                    perspective.Position;

                perspective.UpDirection =
                    up;
            }

            //
            // ========================================================
            // Orthographic camera
            // ========================================================
            //

            else if (Viewport.Camera is OrthographicCamera orthographic)
            {
                double aspect =
                    viewportWidth /
                    viewportHeight;

                //
                // Width is the horizontal size of the orthographic
                // viewing volume.
                //
                // The corresponding vertical size is:
                //
                //     Width / aspect
                //

                double requiredWidth =
                    Math.Max(
                        projectedWidth,
                        projectedHeight * aspect);

                requiredWidth *=
                    fitMargin;

                //
                // Preserve the camera's current distance from the
                // model, but make sure it remains positioned on the
                // current viewing axis.
                //

                Vector3D cameraOffset =
                    orthographic.Position -
                    center;

                double cameraDistance =
                    cameraOffset.Length;

                if (cameraDistance < 1e-6)
                {
                    double modelSize =
                        Math.Sqrt(
                            bounds.Value.SizeX * bounds.Value.SizeX +
                            bounds.Value.SizeY * bounds.Value.SizeY +
                            bounds.Value.SizeZ * bounds.Value.SizeZ);

                    cameraDistance =
                        Math.Max(
                            modelSize * 10.0,
                            1000.0);
                }

                orthographic.Position = center - forward * cameraDistance;

                orthographic.LookDirection = center - orthographic.Position;

                orthographic.UpDirection = up;

                //
                // This is the actual orthographic zoom/fit value.
                //

                orthographic.Width = requiredWidth;
            }
        }

        public void ResetCamera()
        {
            Viewport.Reset();
            Viewport.Camera?.Reset();

            // just recreate the camera to ensure it is in a known state
            CreateCamera();

            FitModel(ModelBounds);
        }

        public void SetCameraView(ModelViewDirection viewDirection)
        {
            if (Viewport.Camera == null)
                return;

            Vector3D forward;

            switch (viewDirection)
            {
                case ModelViewDirection.Front:
                    forward = new Vector3D(0, 0, -1);
                    break;

                case ModelViewDirection.Back:
                    forward = new Vector3D(0, 0, 1);
                    break;

                case ModelViewDirection.Left:
                    forward = new Vector3D(-1, 0, 0);
                    break;

                case ModelViewDirection.Right:
                    forward = new Vector3D(1, 0, 0);
                    break;

                case ModelViewDirection.Top:
                    forward = new Vector3D(0, -1, 0);
                    break;

                case ModelViewDirection.Bottom:
                    forward = new Vector3D(0, 1, 0);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(viewDirection),
                        viewDirection,
                        null);
            }

            Vector3D up = Viewport.Camera.UpDirection;

            if (up.LengthSquared < 1e-12)
                return;

            forward.Normalize();
            up.Normalize();

            //
            // The selected UpDirection cannot be parallel to
            // the requested view direction.
            //

            double alignment =
                Math.Abs(
                    Vector3D.DotProduct(
                        forward,
                        up));

            if (alignment > 0.999999)
                return;

            Viewport.Camera.LookDirection = forward;

            FitModel(ModelBounds);
        }

        public void ShowViewCube(bool show)
        {
            Viewport.ShowViewCube = show;
        }

        public void ShowCoordinateSystem(bool show)
        {
            Viewport.ShowCoordinateSystem = show;
        }

        public void ShowGrid(bool show)
        {
            if (_floorGrid != null)
            {
                _floorGrid.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                return;
            }

            _floorGrid = CreateFloorGrid();

            Viewport.Items.Add(_floorGrid);
        }

        public void ShowBoundingBox(bool show)
        {
            if (_boundingBox != null)
            {
                _boundingBox.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                return;
            }

            _boundingBox = CreateBoundingBox();

            if (_boundingBox == null)
                return;

            Viewport.Items.Add(_boundingBox);
        }

        public void ShowWireframe(bool show)
        {
            if (_loadedScene != null)
            {
                foreach (MeshNode meshNode in _loadedScene.Root.Traverse().OfType<MeshNode>())
                {
                    meshNode.FillMode = show ? FillMode.Wireframe : FillMode.Solid;
                }
            }
        }

        public void SetAmbientLightIntensity(float _ambientLightIntensity)
        {
            AmbientLight.Color = CreateGrayscaleColor(_ambientLightIntensity);
        }

        public void SetKeyLightIntensity(float _keyLightIntensity)
        {
            KeyLight.Color = CreateGrayscaleColor(_keyLightIntensity);
        }

        public void SetFillLightIntensity(float _fillLightIntensity)
        {
            FillLight.Color = CreateGrayscaleColor(_fillLightIntensity);
        }

        private static Color CreateGrayscaleColor(float intensity)
        {
            byte value = (byte)Math.Clamp(
                    intensity * 255.0,
                    0.0,
                    255.0);

            return Color.FromRgb(value, value, value);
        }

        // -------------------------
        // Initialization Methods
        // -------------------------

        private void CreateCamera()
        {
            var camera = new PerspectiveCamera
            {
                Position = new Point3D(5, 5, 5),
                LookDirection = new Vector3D(-5, -5, -5),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45,
                NearPlaneDistance = PerspectiveNearPlane,
                FarPlaneDistance = CameraFarPlane
            };

            Viewport.Camera = camera;
            Viewport.DefaultCamera = camera;
        }

        private Rect3D? CalculateModelBounds()
        {
            _modelBounds = null;

            if (_loadedScene?.Root == null)
                return null;

            bool hasBounds = false;

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double minZ = double.MaxValue;

            double maxX = double.MinValue;
            double maxY = double.MinValue;
            double maxZ = double.MinValue;

            foreach (SceneNode node in _loadedScene.Root.Traverse())
            {
                if (node is not MeshNode meshNode)
                    continue;

                if (meshNode.Geometry is not MeshGeometry3D geometry)
                    continue;

                if (geometry.Positions == null || geometry.Positions.Count == 0)
                    continue;

                    foreach (var position in geometry.Positions)
                {
                    minX = Math.Min(minX, position.X);
                    minY = Math.Min(minY, position.Y);
                    minZ = Math.Min(minZ, position.Z);

                    maxX = Math.Max(maxX, position.X);
                    maxY = Math.Max(maxY, position.Y);
                    maxZ = Math.Max(maxZ, position.Z);

                    hasBounds = true;
                }
            }

            if (!hasBounds)
                return null;

            Rect3D modelBounds = new(
                minX,
                minY,
                minZ,
                maxX - minX,
                maxY - minY,
                maxZ - minZ);

            Debug.WriteLine($"Model bounds: {modelBounds}");

            return modelBounds;
        }

        private const double FloorGridExtent = 1000.0;
        private const double FloorGridSpacing = 10.0;

        private static LineGeometryModel3D CreateFloorGrid()
        {
            var builder = new LineBuilder();

            for (double x = -FloorGridExtent; x <= FloorGridExtent; x += FloorGridSpacing)
            {
                builder.AddLine(
                    new Vector3((float)x, 0, (float)-FloorGridExtent),
                    new Vector3((float)x, 0, (float)FloorGridExtent));
            }

            for (double z = -FloorGridExtent; z <= FloorGridExtent; z += FloorGridSpacing)
            {
                builder.AddLine(
                    new Vector3((float)-FloorGridExtent, 0, (float)z),
                    new Vector3((float)FloorGridExtent, 0, (float)z));
            }

            return new LineGeometryModel3D
            {
                Geometry = builder.ToLineGeometry3D(),
                Color = Colors.LightGray,
                Thickness = 0.25
            };
        }

        private LineGeometryModel3D? CreateBoundingBox()
        {
            if (_loadedScene?.Root == null)
                return null;

            if (!_modelBounds.HasValue)
                return null;

            Rect3D bounds = _modelBounds.Value;

            double minX = bounds.X;
            double minY = bounds.Y;
            double minZ = bounds.Z;

            double maxX = bounds.X + bounds.SizeX;
            double maxY = bounds.Y + bounds.SizeY;
            double maxZ = bounds.Z + bounds.SizeZ;

            var builder = new LineBuilder();

            Vector3 p000 = new((float)minX, (float)minY, (float)minZ);
            Vector3 p001 = new((float)minX, (float)minY, (float)maxZ);
            Vector3 p010 = new((float)minX, (float)maxY, (float)minZ);
            Vector3 p011 = new((float)minX, (float)maxY, (float)maxZ);

            Vector3 p100 = new((float)maxX, (float)minY, (float)minZ);
            Vector3 p101 = new((float)maxX, (float)minY, (float)maxZ);
            Vector3 p110 = new((float)maxX, (float)maxY, (float)minZ);
            Vector3 p111 = new((float)maxX, (float)maxY, (float)maxZ);

            // Bottom.
            builder.AddLine(p000, p100);
            builder.AddLine(p100, p101);
            builder.AddLine(p101, p001);
            builder.AddLine(p001, p000);

            // Top.
            builder.AddLine(p010, p110);
            builder.AddLine(p110, p111);
            builder.AddLine(p111, p011);
            builder.AddLine(p011, p010);

            // Vertical edges.
            builder.AddLine(p000, p010);
            builder.AddLine(p100, p110);
            builder.AddLine(p101, p111);
            builder.AddLine(p001, p011);

            return new LineGeometryModel3D
            {
                Geometry = builder.ToLineGeometry3D(),
                Color = Colors.Red,
                Thickness = 1.0
            };
        }

        public BitmapSource? CreateSnapshot(int width, int height)
        {
            return Viewport.RenderBitmap(width, height);
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
