using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.VisualBasic.ApplicationServices;
using RealmStudioShapeRenderingLib;
using SkiaSharp;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Media.Media3D;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;


namespace RealmStudioX._3D.Models
{
    public sealed class HeightMapTerrain3D
    {
        private MeshGeometry3D? _geometry;
        private MeshGeometryModel3D? _model;

        private MapHeightMap? _heightMap;
        private float[,]? _elevationMap;

        private int _width;
        private int _height;

        private float _minimumElevation;
        private float _maximumElevation;
        private float _elevationScale;

        private PhongMaterial? _material;

        private byte[]? _texturePixels;

        private int _textureModifiedLeft;
        private int _textureModifiedTop;
        private int _textureModifiedRight;
        private int _textureModifiedBottom;

        private bool _textureModified;

        public MeshGeometryModel3D? Model => _model;

        public Rect3D Bounds { get; private set; }

        private Func<float, float, bool>? _isInsideLandform;

        public void Create(
            MapHeightMap heightMap,
            float minimumElevation,
            float maximumElevation,
            float elevationScale,
            Func<float, float, bool>? isInsideLandform = null)
        {
            ArgumentNullException.ThrowIfNull(heightMap);

            _heightMap = heightMap;
            _elevationMap = heightMap.HeightMap;

            _isInsideLandform = isInsideLandform;

            ArgumentNullException.ThrowIfNull(_elevationMap);

            _width = _elevationMap.GetLength(0);
            _height = _elevationMap.GetLength(1);

            if (_width < 2 || _height < 2)
                throw new ArgumentException(
                    "Heightmap must be at least 2 x 2 pixels.",
                    nameof(heightMap));

            _minimumElevation = minimumElevation;
            _maximumElevation = maximumElevation;
            _elevationScale = elevationScale;

            CreateMesh();
            CreateTexture();
            CreateModel();
        }

        public void UpdateRegion(int left, int top, int right, int bottom)
        {
            if (_geometry == null || _elevationMap == null)
                return;

            left = Math.Clamp(left, 0, _width - 1);
            right = Math.Clamp(right, 0, _width - 1);
            top = Math.Clamp(top, 0, _height - 1);
            bottom = Math.Clamp(bottom, 0, _height - 1);

            if (left > right || top > bottom)
                return;

            UpdatePositions(left, top, right, bottom);
            UpdateNormals(left, top, right, bottom);
            UpdateTexture(left, top, right, bottom);

            _geometry.UpdateVertices();
        }

        private void UpdatePositions(int left, int top, int right, int bottom)
        {
            if (_geometry == null || _elevationMap == null || _geometry.Positions == null)
                return;

            Vector3Collection positions = _geometry.Positions;

            float halfWidth = (_width - 1) / 2.0f;
            float halfHeight = (_height - 1) / 2.0f;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    float elevation = _elevationMap[x, y];

                    float normalizedElevation = NormalizeElevation(
                        elevation,
                        _minimumElevation,
                        _maximumElevation);

                    int index = GetVertexIndex(x, y);

                    Vector3 position = positions[index];

                    position.X = x - halfWidth;
                    position.Y = normalizedElevation * _elevationScale;
                    position.Z = y - halfHeight;

                    positions[index] = position;
                }
            }
        }

        private void UpdateNormals(
            int left,
            int top,
            int right,
            int bottom)
        {
            if (_geometry == null || _elevationMap == null || _geometry.Normals == null)
            {
                return;
            }

            Vector3Collection normals = _geometry.Normals;

            int normalLeft = Math.Max(0, left - 1);
            int normalTop = Math.Max(0, top - 1);
            int normalRight = Math.Min(_width - 1, right + 1);
            int normalBottom = Math.Min(_height - 1, bottom + 1);

            for (int x = normalLeft; x <= normalRight; x++)
            {
                for (int y = normalTop; y <= normalBottom; y++)
                {
                    normals[GetVertexIndex(x, y)] =
                        CalculateNormal(x, y);
                }
            }
        }

        private void CreateTexture()
        {
            if (_elevationMap == null || _heightMap == null)
                return;

            _texturePixels = new byte[
                checked(_width * _height * 4)];

            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    SetTexturePixel(x, y);
                }
            }

            _textureModified = true;
            _textureModifiedLeft = 0;
            _textureModifiedTop = 0;
            _textureModifiedRight = _width - 1;
            _textureModifiedBottom = _height - 1;
        }

        private void UpdateTexture(
            int left,
            int top,
            int right,
            int bottom)
        {
            if (_texturePixels == null ||
                _elevationMap == null ||
                _heightMap == null)
                return;

            left = Math.Clamp(left, 0, _width - 1);
            right = Math.Clamp(right, 0, _width - 1);
            top = Math.Clamp(top, 0, _height - 1);
            bottom = Math.Clamp(bottom, 0, _height - 1);

            if (left > right || top > bottom)
                return;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    SetTexturePixel(x, y);
                }
            }

            MarkTextureModified(left, top, right, bottom);
        }

        private void MarkTextureModified(
            int left,
            int top,
            int right,
            int bottom)
        {
            if (!_textureModified)
            {
                _textureModifiedLeft = left;
                _textureModifiedTop = top;
                _textureModifiedRight = right;
                _textureModifiedBottom = bottom;
                _textureModified = true;
                return;
            }

            _textureModifiedLeft = Math.Min(_textureModifiedLeft, left);
            _textureModifiedTop = Math.Min(_textureModifiedTop, top);
            _textureModifiedRight = Math.Max(_textureModifiedRight, right);
            _textureModifiedBottom = Math.Max(_textureModifiedBottom, bottom);
        }

        private float GetTerrainHeight(int x, int y)
        {
            x = Math.Clamp(x, 0, _width - 1);
            y = Math.Clamp(y, 0, _height - 1);

            float elevation = _elevationMap![x, y];

            float normalizedElevation = NormalizeElevation(
                elevation,
                _minimumElevation,
                _maximumElevation);

            return normalizedElevation;
        }

        private SKColor GetHeightMapColor(float elevation)
        {
            float normalizedElevation = NormalizeElevation(
                elevation,
                _minimumElevation,
                _maximumElevation);

            if (_heightMap!.HeightMapPalette != null)
            {
                return MapHeightMap.GetHypsometricColor(normalizedElevation, _heightMap!.HeightMapPalette);
            }

            return SKColors.White;
        }

        private MemoryStream? _textureStream;

        private void CreateModel()
        {
            if (_geometry == null ||
                _texturePixels == null)
                return;

            using SKBitmap bitmap = new(
                _width,
                _height,
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul);

            Marshal.Copy(
                _texturePixels,
                0,
                bitmap.GetPixels(),
                _texturePixels.Length);

            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(
                SKEncodedImageFormat.Png,
                100);

            // The stream must remain alive while the material is using it.
            _textureStream?.Dispose();
            _textureStream = new MemoryStream(data.ToArray());

            _material = new PhongMaterial
            {
                AmbientColor = Color.White,
                DiffuseColor = Color.White,
                SpecularColor = Color.Black,
                SpecularShininess = 0,
                DiffuseMap = _textureStream
            };

            _model = new MeshGeometryModel3D
            {
                Geometry = _geometry,
                Material = _material
            };
        }

        private void CreateMesh()
        {
            Vector3Collection positions = new();
            Vector3Collection normals = new();
            Vector2Collection textureCoordinates = new();

            int vertexCount = checked(_width * _height);

            positions.Capacity = vertexCount;
            normals.Capacity = vertexCount;
            textureCoordinates.Capacity = vertexCount;

            float halfWidth = (_width - 1) / 2.0f;
            float halfHeight = (_height - 1) / 2.0f;

            for (int x = 0; x < _width; x++)
            {
                float u = (float)x / (_width - 1);

                for (int y = 0; y < _height; y++)
                {
                    float v = (float)y / (_height - 1);

                    float elevation = _elevationMap![x, y];

                    float normalizedElevation = NormalizeElevation(
                        elevation,
                        _minimumElevation,
                        _maximumElevation);

                    float px = x - halfWidth;
                    float py = normalizedElevation * _elevationScale;
                    float pz = y - halfHeight;

                    positions.Add(new Vector3(px, py, pz));

                    // The actual normals are calculated after all positions
                    // have been created.
                    normals.Add(Vector3.UnitY);

                    textureCoordinates.Add(new Vector2(u, v));
                }
            }

            // Calculate the initial terrain normals now that all
            // vertex positions are available.
            CalculateInitialNormals(normals);

            float minimumY = -_elevationScale;
            float maximumY = _elevationScale;

            Bounds = new Rect3D(
                -halfWidth,
                minimumY,
                -halfHeight,
                _width - 1,
                maximumY - minimumY,
                _height - 1);

            IntCollection indices = new();

            int triangleCount =
                checked((_width - 1) * (_height - 1) * 2);

            indices.Capacity = checked(triangleCount * 3);

            for (int x = 0; x < _width - 1; x++)
            {
                for (int y = 0; y < _height - 1; y++)
                {
                    int i0 = GetVertexIndex(x, y);
                    int i1 = GetVertexIndex(x + 1, y);
                    int i2 = GetVertexIndex(x, y + 1);
                    int i3 = GetVertexIndex(x + 1, y + 1);

                    indices.Add(i0);
                    indices.Add(i2);
                    indices.Add(i1);

                    indices.Add(i1);
                    indices.Add(i2);
                    indices.Add(i3);
                }
            }

            _geometry = new MeshGeometry3D
            {
                IsDynamic = true,
                Positions = positions,
                Normals = normals,
                Indices = indices,
                TextureCoordinates = textureCoordinates
            };
        }

        private void CalculateInitialNormals(
            Vector3Collection normals)
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    normals[GetVertexIndex(x, y)] = CalculateNormal(x, y);
                }
            }
        }

        private Vector3 CalculateNormal(int x, int y)
        {
            float leftElevation = GetTerrainHeight(x - 1, y);
            float rightElevation = GetTerrainHeight(x + 1, y);
            float downElevation = GetTerrainHeight(x, y - 1);
            float upElevation = GetTerrainHeight(x, y + 1);

            float dx =
                (rightElevation - leftElevation) *
                _elevationScale /
                2.0f;

            float dz =
                (upElevation - downElevation) *
                _elevationScale /
                2.0f;

            Vector3 normal = new(-dx, 1.0f, -dz);

            Vector3.Normalize(normal);

            return normal;
        }

        private static float NormalizeElevation(
            float elevation,
            float minimumElevation,
            float maximumElevation)
        {
            if (elevation < 0.0f)
            {
                if (minimumElevation >= 0.0f)
                    return 0.0f;

                return Math.Clamp(
                    elevation / Math.Abs(minimumElevation),
                    -1.0f,
                    0.0f);
            }

            if (maximumElevation <= 0.0f)
                return 0.0f;

            return Math.Clamp(
                elevation / maximumElevation,
                0.0f,
                1.0f);
        }

        private TextureModel? CreateTextureModel()
        {
            if (_texturePixels == null)
                return null;

            using SKBitmap bitmap = new(
                _width,
                _height,
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul);

            IntPtr pixels = bitmap.GetPixels();

            Marshal.Copy(
                _texturePixels,
                0,
                pixels,
                _texturePixels.Length);

            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(
                SKEncodedImageFormat.Png,
                100);

            MemoryStream stream = new(data.ToArray());

            return new TextureModel(stream);
        }

        private void SetTexturePixel(int x, int y)
        {
            if (_texturePixels == null ||
                _elevationMap == null ||
                _heightMap == null)
                return;

            SKColor color = SKColor.Empty;

            if (_isInsideLandform != null &&
                !_isInsideLandform(x, y))
            {
                color = SKColors.Black;
            }
            else
            {
                float elevation = _elevationMap[x, y];
                color = GetHeightMapColor(elevation);
            }

            int index = checked((y * _width + x) * 4);

            _texturePixels[index + 0] = color.Red;
            _texturePixels[index + 1] = color.Green;
            _texturePixels[index + 2] = color.Blue;
            _texturePixels[index + 3] = color.Alpha;
        }

        private int GetVertexIndex(int x, int y)
        {
            return x * _height + y;
        }

    }

}
