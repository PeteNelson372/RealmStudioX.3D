using HelixToolkit.Wpf;
using RealmStudioShapeRenderingLib;
using RealmStudioShapeRenderingLib.Logging;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RealmStudioX._3D.Views.Controls
{
    /// <summary>
    /// Interaction logic for ModelViewerControl.xaml
    /// </summary>
    public partial class ModelViewerControl : UserControl, I3DModelViewer
    {
        private Rect3D? _modelBounds;

        public Rect3D? ModelBounds => _modelBounds;

        private const double PerspectiveNearPlane = 0.1;
        private const double OrthographicNearPlane = -1_000_000.0;
        private const double CameraFarPlane = 1_000_000.0;

        private bool _showGrid = false;
        private bool _showBoundingBox = false;

        private readonly LinesVisual3D _boundingBoxVisual = new()
        {
            Color = Colors.Red,
            Thickness = 1.0
        };

        private readonly ModelVisual3D GridlinesModel = new();
        private readonly GridLinesVisual3D GridLines = new()
        {
            Center = new Point3D(0, -0.5, 0),
            Normal = new Vector3D(0, 1, 0),
            Width = 1000,
            Length = 1000,
            MinorDistance = 10,
            MajorDistance = 50,
            Thickness = 0.2,
            Fill = System.Windows.Media.Brushes.Gray
        };

        private bool _showWireframe;

        private readonly LinesVisual3D _wireframeVisual = new()
        {
            Color = Colors.White,
            Thickness = 1.0
        };

        public ModelViewerControl()
        {
            InitializeComponent();
            InitializeCamera();

            HelixTKViewport.PanGesture = default!;
            HelixTKViewport.PanGesture2 = new System.Windows.Input.MouseGesture(System.Windows.Input.MouseAction.LeftClick);

            HelixTKViewport.RotateGesture = default!;
            HelixTKViewport.RotateGesture2 = default!;
            HelixTKViewport.RotateGesture2 = new System.Windows.Input.MouseGesture(System.Windows.Input.MouseAction.RightClick);

            HelixTKViewport.ZoomSensitivity = 1.0;

            HelixTKViewport.InfiniteSpin = true;

            // add gridlines
            GridlinesModel.SetName("GridLines");
            GridlinesModel.Children.Add(GridLines);

            ShowGrid(_showGrid);
        }

        private void InitializeCamera()
        {
            // Y Up is the default orientation

            HelixTKViewport.Camera = new PerspectiveCamera
            {
                Position = new Point3D(5, 5, 5),
                LookDirection = new Vector3D(-5, -5, -5),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45,
                NearPlaneDistance = 0.01,
                FarPlaneDistance = 10000
            };
        }

        private bool _isModelLoaded = false;
        public bool IsModelLoaded
        {
            get { return _isModelLoaded; }
            set { SetProperty(ref _isModelLoaded, value); }
        }

        public void FitModel(Rect3D? bounds)
        {
            if (HelixTKViewport.Camera == null)
                return;

            if (bounds == null)
            {
                return;
            }

            double viewportWidth = HelixTKViewport.ActualWidth;
            double viewportHeight = HelixTKViewport.ActualHeight;

            if (viewportWidth <= 0.0 ||
                viewportHeight <= 0.0)
            {
                return;
            }

            Point3D center = new(
                bounds.Value.X + bounds.Value.SizeX / 2.0,
                bounds.Value.Y + bounds.Value.SizeY / 2.0,
                bounds.Value.Z + bounds.Value.SizeZ / 2.0);

            Vector3D forward = HelixTKViewport.Camera.LookDirection;

            if (forward.LengthSquared < 1e-12)
                return;

            forward.Normalize();

            Vector3D up = HelixTKViewport.Camera.UpDirection;

            if (up.LengthSquared < 1e-12)
                up = new Vector3D(0, 1, 0);
            else
                up.Normalize();

            Vector3D right =
                Vector3D.CrossProduct(forward, up);

            if (right.LengthSquared < 1e-12)
                return;

            right.Normalize();

            //
            // Project the eight corners of the bounding box onto
            // the camera's right, up, and forward vectors.
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

            double minHorizontal = double.MaxValue;
            double maxHorizontal = double.MinValue;

            double minVertical = double.MaxValue;
            double maxVertical = double.MinValue;

            double minDepth = double.MaxValue;
            double maxDepth = double.MinValue;

            foreach (Point3D corner in corners)
            {
                Vector3D offset = corner - center;

                double horizontal =
                    Vector3D.DotProduct(offset, right);

                double vertical =
                    Vector3D.DotProduct(offset, up);

                double depth =
                    Vector3D.DotProduct(offset, forward);

                minHorizontal =
                    Math.Min(minHorizontal, horizontal);

                maxHorizontal =
                    Math.Max(maxHorizontal, horizontal);

                minVertical =
                    Math.Min(minVertical, vertical);

                maxVertical =
                    Math.Max(maxVertical, vertical);

                minDepth =
                    Math.Min(minDepth, depth);

                maxDepth =
                    Math.Max(maxDepth, depth);
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

            const double fitMargin = 1.20;

            //
            // Perspective projection
            //

            if (HelixTKViewport.Camera is PerspectiveCamera perspective)
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
                    viewportWidth / viewportHeight;

                double horizontalFov =
                    2.0 *
                    Math.Atan(
                        Math.Tan(verticalFov / 2.0) *
                        aspect);

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
                // The camera must also be far enough away to
                // accommodate the model's depth.
                //

                distance += depthExtent / 2.0;

                distance *= fitMargin;

                perspective.Position =
                    center - forward * distance;

                perspective.LookDirection =
                    center - perspective.Position;

                perspective.UpDirection =
                    up;

                return;
            }

            //
            // Orthographic projection
            //

            if (HelixTKViewport.Camera is OrthographicCamera orthographic)
            {
                double aspect =
                    viewportWidth / viewportHeight;

                //
                // OrthographicCamera.Width is the horizontal
                // extent visible through the viewport.
                //
                // Therefore the vertical extent is:
                //
                //     Width / aspect
                //
                // We need the width to accommodate either the
                // projected horizontal size or the projected
                // vertical size converted to horizontal space.
                //

                double requiredWidth =
                    Math.Max(
                        projectedWidth,
                        projectedHeight * aspect);

                requiredWidth *= fitMargin;

                orthographic.Position =
                    center - forward * GetOrthographicCameraDistance(
                        orthographic,
                        center,
                        bounds.Value);

                orthographic.LookDirection =
                    center - orthographic.Position;

                orthographic.UpDirection =
                    up;

                orthographic.Width =
                    requiredWidth;
            }
        }

        private static double GetOrthographicCameraDistance(
            OrthographicCamera camera,
            Point3D center,
            Rect3D bounds)
        {
            Vector3D offset =
                camera.Position - center;

            double distance = offset.Length;

            if (distance > 1e-6)
                return distance;

            double modelSize =
                Math.Sqrt(
                    bounds.SizeX * bounds.SizeX +
                    bounds.SizeY * bounds.SizeY +
                    bounds.SizeZ * bounds.SizeZ);

            return Math.Max(
                modelSize * 10.0,
                1000.0);
        }

        public void LoadModel(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                return;
            }

            try
            {
                ModelImporter importer = new();

                Model3D? model = importer.Load(fileName);

                if (model == null)
                {
                    return;
                }

                ModelGroup.Children.Clear();

                if (model is Model3DGroup modelGroup)
                {
                    foreach (Model3D child in modelGroup.Children)
                    {
                        ModelGroup.Children.Add(child);
                    }
                }
                else
                {
                    ModelGroup.Children.Add(model);
                }

                _isModelLoaded = true;

                CalculateModelBounds();
                FitModel(_modelBounds);
                ShowGrid(_showGrid);

                UpdateBoundingBox();
                UpdateWireframe();

                HelixTKViewport.Children.Add(_boundingBoxVisual);
                ShowBoundingBox(_showBoundingBox);

                HelixTKViewport.Children.Add(_wireframeVisual);
                ShowWireframe(_showWireframe);
            }
            catch (Exception ex)
            {
                _isModelLoaded = false;

                // RealmStudioX logging
                RealmStudioXLogger.Error(ex.Message);
            }
        }

        private void CalculateModelBounds()
        {
            Rect3D bounds = Rect3D.Empty;

            foreach (Model3D model in ModelGroup.Children)
            {
                AddModelBounds(model, ref bounds);
            }

            _modelBounds = bounds.IsEmpty
                ? null
                : bounds;
        }

        private static void AddModelBounds(Model3D model, ref Rect3D bounds)
        {
            if (model is GeometryModel3D geometryModel &&
                geometryModel.Geometry != null)
            {
                Rect3D geometryBounds =
                    geometryModel.Geometry.Bounds;

                if (!geometryBounds.IsEmpty)
                    bounds.Union(geometryBounds);
            }

            if (model is Model3DGroup group)
            {
                foreach (Model3D child in group.Children)
                {
                    AddModelBounds(child, ref bounds);
                }
            }
        }

        private void UpdateBoundingBox()
        {
            if (_modelBounds == null || _modelBounds.Value.IsEmpty)
            {
                _boundingBoxVisual.Points = [];
                return;
            }

            Rect3D bounds = _modelBounds.Value;

            double x0 = bounds.X;
            double x1 = bounds.X + bounds.SizeX;

            double y0 = bounds.Y;
            double y1 = bounds.Y + bounds.SizeY;

            double z0 = bounds.Z;
            double z1 = bounds.Z + bounds.SizeZ;

            _boundingBoxVisual.Points = new Point3DCollection
            {
                // Bottom
                new Point3D(x0, y0, z0),
                new Point3D(x1, y0, z0),

                new Point3D(x1, y0, z0),
                new Point3D(x1, y0, z1),

                new Point3D(x1, y0, z1),
                new Point3D(x0, y0, z1),

                new Point3D(x0, y0, z1),
                new Point3D(x0, y0, z0),

                // Top
                new Point3D(x0, y1, z0),
                new Point3D(x1, y1, z0),

                new Point3D(x1, y1, z0),
                new Point3D(x1, y1, z1),

                new Point3D(x1, y1, z1),
                new Point3D(x0, y1, z1),

                new Point3D(x0, y1, z1),
                new Point3D(x0, y1, z0),

                // Vertical edges
                new Point3D(x0, y0, z0),
                new Point3D(x0, y1, z0),

                new Point3D(x1, y0, z0),
                new Point3D(x1, y1, z0),

                new Point3D(x1, y0, z1),
                new Point3D(x1, y1, z1),

                new Point3D(x0, y0, z1),
                new Point3D(x0, y1, z1)
            };
        }


        private void UpdateWireframe()
        {
            Point3DCollection points = new();

            foreach (Model3D model in ModelGroup.Children)
                AddWireframe(model, points);

            _wireframeVisual.Points = points;
        }

        private static void AddWireframe(Model3D model, Point3DCollection points)
        {
            if (model is GeometryModel3D geometryModel &&
                geometryModel.Geometry is MeshGeometry3D mesh)
            {
                AddMeshWireframe(mesh, points);
            }

            if (model is Model3DGroup group)
            {
                foreach (Model3D child in group.Children)
                    AddWireframe(child, points);
            }
        }



        private static void AddMeshWireframe(
            MeshGeometry3D mesh,
            Point3DCollection points)
        {
            Point3DCollection positions = mesh.Positions;
            Int32Collection indices = mesh.TriangleIndices;

            HashSet<(int A, int B)> edges = new();

            for (int i = 0; i < indices.Count; i += 3)
            {
                AddEdge(indices[i], indices[i + 1]);
                AddEdge(indices[i + 1], indices[i + 2]);
                AddEdge(indices[i + 2], indices[i]);
            }

            void AddEdge(int a, int b)
            {
                int min = Math.Min(a, b);
                int max = Math.Max(a, b);

                if (!edges.Add((min, max)))
                    return;

                points.Add(positions[a]);
                points.Add(positions[b]);
            }
        }

        public void ResetCamera()
        {
            InitializeCamera();
            FitModel(_modelBounds);
        }

        public void SetCameraProjection(CameraProjection projection)
        {
            if (HelixTKViewport.Camera == null)
                return;

            Point3D position =
                HelixTKViewport.Camera.Position;

            Vector3D lookDirection =
                HelixTKViewport.Camera.LookDirection;

            Vector3D upDirection =
                HelixTKViewport.Camera.UpDirection;

            switch (projection)
            {
                case CameraProjection.Perspective:
                    {
                        if (HelixTKViewport.Camera is PerspectiveCamera)
                            return;

                        HelixTKViewport.Camera = new PerspectiveCamera
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
                        if (HelixTKViewport.Camera is OrthographicCamera)
                            return;

                        HelixTKViewport.Camera = new OrthographicCamera
                        {
                            Position = position,
                            LookDirection = lookDirection,
                            UpDirection = upDirection,
                            Width = 10,
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

            FitModel(_modelBounds);
        }

        public void SetCameraView(ModelViewDirection viewDirection)
        {
            if (HelixTKViewport.Camera == null)
                return;
            var forward = viewDirection switch
            {
                ModelViewDirection.Front => new Vector3D(0, 0, -1),
                ModelViewDirection.Back => new Vector3D(0, 0, 1),
                ModelViewDirection.Left => new Vector3D(-1, 0, 0),
                ModelViewDirection.Right => new Vector3D(1, 0, 0),
                ModelViewDirection.Top => new Vector3D(0, -1, 0),
                ModelViewDirection.Bottom => new Vector3D(0, 1, 0),
                _ => throw new ArgumentOutOfRangeException(
                                        nameof(viewDirection),
                                        viewDirection,
                                        null),
            };

            Vector3D up = HelixTKViewport.Camera.UpDirection;

            if (up.LengthSquared < 1e-12)
                return;

            forward.Normalize();
            up.Normalize();

            //
            // The selected UpDirection cannot be parallel to
            // the requested view direction.
            //

            double alignment = Math.Abs(Vector3D.DotProduct(forward, up));

            if (alignment > 0.999999)
                return;

            HelixTKViewport.Camera.LookDirection = forward;

            FitModel(_modelBounds);
        }

        public void SetUpDirection(ModelUpDirection upDirection)
        {
            if (HelixTKViewport.Camera == null)
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

            Vector3D forward = HelixTKViewport.Camera.LookDirection;

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

            HelixTKViewport.Camera.UpDirection = up;

            FitModel(_modelBounds);
        }

        public void ShowBoundingBox(bool show)
        {
            _showBoundingBox = show;

            if (show)
            {
                if (!HelixTKViewport.Children.Contains(_boundingBoxVisual))
                {
                    HelixTKViewport.Children.Add(_boundingBoxVisual);
                }
            }
            else
            {
                HelixTKViewport.Children.Remove(_boundingBoxVisual);
            }
        }

        public void ShowCoordinateSystem(bool show)
        {
            HelixTKViewport.ShowCoordinateSystem = show;
        }

        public void ShowGrid(bool show)
        {
            _showGrid = show;

            if (show)
            {
                if (!HelixTKViewport.Children.Contains(GridlinesModel))
                {
                    HelixTKViewport.Children.Add(GridlinesModel);
                }
            }
            else
            {
                HelixTKViewport.Children.Remove(GridlinesModel);
            }
        }

        public void ShowViewCube(bool show)
        {
            HelixTKViewport.ShowViewCube = show;
        }

        public void ShowWireframe(bool show)
        {
            _showWireframe = show;

            if (show)
            {
                if (!HelixTKViewport.Children.Contains(_wireframeVisual))
                {
                    HelixTKViewport.Children.Add(_wireframeVisual);
                }
            }
            else
            {
                HelixTKViewport.Children.Remove(_wireframeVisual);
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
