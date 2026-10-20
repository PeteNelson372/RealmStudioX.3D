using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using RealmStudioShapeRenderingLib;
using SkiaSharp;
using System.Numerics;
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

        private int _meshWidth;
        private int _meshHeight;

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

        private Func<int, int, bool>? _isInsideLandform;

        private const int MaxTerrainSamples = 3_000_000;

        public void Create(
            MapHeightMap heightMap,
            float minimumElevation,
            float maximumElevation,
            float elevationScale,
            Func<int, int, bool>? isInsideLandform = null)
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

            CalculateMeshDimensions();

            CreateMesh();
            CreateTexture();
            CreateModel();
        }

        private void CalculateMeshDimensions()
        {
            long sourceSamples = checked((long)_width * _height);

            if (sourceSamples <= MaxTerrainSamples)
            {
                _meshWidth = _width;
                _meshHeight = _height;
                return;
            }

            double scale = Math.Sqrt(
                MaxTerrainSamples / (double)sourceSamples);

            _meshWidth = Math.Max(
                2,
                (int)Math.Floor(_width * scale));

            _meshHeight = Math.Max(
                2,
                (int)Math.Floor(_height * scale));

            // Make absolutely certain that rounding/flooring hasn't
            // put us over the requested maximum.
            while ((long)_meshWidth * _meshHeight > MaxTerrainSamples)
            {
                if (_meshWidth >= _meshHeight)
                    _meshWidth--;
                else
                    _meshHeight--;
            }
        }

        private int GetSourceX(int meshX)
        {
            if (_meshWidth <= 1)
                return 0;

            return (int)MathF.Round(
                meshX * (_width - 1) /
                (float)(_meshWidth - 1));
        }

        private int GetSourceY(int meshY)
        {
            if (_meshHeight <= 1)
                return 0;

            return (int)MathF.Round(
                meshY * (_height - 1) /
                (float)(_meshHeight - 1));
        }

        private int GetMeshX(int sourceX)
        {
            if (_width <= 1)
                return 0;

            return (int)MathF.Round(
                sourceX * (_meshWidth - 1) /
                (float)(_width - 1));
        }

        private int GetMeshY(int sourceY)
        {
            if (_height <= 1)
                return 0;

            return (int)MathF.Round(
                sourceY * (_meshHeight - 1) /
                (float)(_height - 1));
        }

        private static (int width, int height) CalculateTerrainDimensions(
            int sourceWidth,
            int sourceHeight)
        {
            long sourceSamples = (long)sourceWidth * sourceHeight;

            if (sourceSamples <= MaxTerrainSamples)
                return (sourceWidth, sourceHeight);

            double scale = Math.Sqrt(
                MaxTerrainSamples / (double)sourceSamples);

            int width = Math.Max(2, (int)Math.Floor(sourceWidth * scale));
            int height = Math.Max(2, (int)Math.Floor(sourceHeight * scale));

            return (width, height);
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

            UpdateTextureModel();
        }

        private void GetMeshRegionFromSourceRegion(
            int left,
            int top,
            int right,
            int bottom,
            out int meshLeft,
            out int meshTop,
            out int meshRight,
            out int meshBottom)
        {
            left = Math.Clamp(left, 0, _width - 1);
            right = Math.Clamp(right, 0, _width - 1);
            top = Math.Clamp(top, 0, _height - 1);
            bottom = Math.Clamp(bottom, 0, _height - 1);

            // Expand the mapping slightly so that a source region
            // between two sampled mesh points still updates a nearby
            // mesh point.
            meshLeft = Math.Max(
                0,
                GetMeshX(left) - 1);

            meshTop = Math.Max(
                0,
                GetMeshY(top) - 1);

            meshRight = Math.Min(
                _meshWidth - 1,
                GetMeshX(right) + 1);

            meshBottom = Math.Min(
                _meshHeight - 1,
                GetMeshY(bottom) + 1);
        }

        private void UpdatePositions(
            int left,
            int top,
            int right,
            int bottom)
        {
            if (_geometry == null ||
                _elevationMap == null ||
                _geometry.Positions == null)
                return;

            Vector3Collection positions = _geometry.Positions;

            GetMeshRegionFromSourceRegion(
                left,
                top,
                right,
                bottom,
                out int meshLeft,
                out int meshTop,
                out int meshRight,
                out int meshBottom);

            float halfWidth = (_width - 1) / 2.0f;
            float halfHeight = (_height - 1) / 2.0f;

            for (int x = meshLeft; x <= meshRight; x++)
            {
                int sourceX = GetSourceX(x);

                for (int y = meshTop; y <= meshBottom; y++)
                {
                    int sourceY = GetSourceY(y);

                    float elevation = _elevationMap[sourceX, sourceY];

                    float normalizedElevation = NormalizeElevation(
                        elevation,
                        _minimumElevation,
                        _maximumElevation);

                    int index = GetVertexIndex(x, y);

                    Vector3 position = positions[index];

                    position.X = sourceX - halfWidth;
                    position.Y = normalizedElevation * _elevationScale;
                    position.Z = sourceY - halfHeight;

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
            if (_geometry == null ||
                _elevationMap == null ||
                _geometry.Normals == null)
            {
                return;
            }

            Vector3Collection normals = _geometry.Normals;

            GetMeshRegionFromSourceRegion(
                left,
                top,
                right,
                bottom,
                out int meshLeft,
                out int meshTop,
                out int meshRight,
                out int meshBottom);

            // Normals depend on neighboring vertices.
            meshLeft = Math.Max(0, meshLeft - 1);
            meshTop = Math.Max(0, meshTop - 1);
            meshRight = Math.Min(_meshWidth - 1, meshRight + 1);
            meshBottom = Math.Min(_meshHeight - 1, meshBottom + 1);

            for (int x = meshLeft; x <= meshRight; x++)
            {
                for (int y = meshTop; y <= meshBottom; y++)
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
                checked(_meshWidth * _meshHeight * 4)];

            for (int x = 0; x < _meshWidth; x++)
            {
                for (int y = 0; y < _meshHeight; y++)
                {
                    SetTexturePixel(x, y);
                }
            }

            _textureModified = true;
            _textureModifiedLeft = 0;
            _textureModifiedTop = 0;
            _textureModifiedRight = _meshWidth - 1;
            _textureModifiedBottom = _meshHeight - 1;
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

            GetMeshRegionFromSourceRegion(
                left,
                top,
                right,
                bottom,
                out int meshLeft,
                out int meshTop,
                out int meshRight,
                out int meshBottom);

            for (int x = meshLeft; x <= meshRight; x++)
            {
                for (int y = meshTop; y <= meshBottom; y++)
                {
                    SetTexturePixel(x, y);
                }
            }

            MarkTextureModified(
                meshLeft,
                meshTop,
                meshRight,
                meshBottom);
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

        private void CreateModel()
        {
            if (_geometry == null ||
                _texturePixels == null)
                return;

            TextureModel texture = CreateTextureModel();

            _material = new PhongMaterial
            {
                AmbientColor = Color.White,
                DiffuseColor = Color.White,
                SpecularColor = Color.Black,
                SpecularShininess = 0,
                DiffuseMap = texture
            };

            _model = new MeshGeometryModel3D
            {
                Geometry = _geometry,
                Material = _material
            };
        }

        private TextureModel CreateTextureModel()
        {
            if (_texturePixels == null)
                throw new InvalidOperationException("Texture pixels have not been created.");

            return new TextureModel(
                _texturePixels,
                SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                _meshWidth,
                _meshHeight);
        }

        private void UpdateTextureModel()
        {
            if (!_textureModified ||
                _texturePixels == null ||
                _material == null)
                return;

            _material.DiffuseMap = new TextureModel(
                _texturePixels,
                SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                _meshWidth,
                _meshHeight);

            _textureModified = false;
        }

        private void CreateMesh()
        {
            Vector3Collection positions = new();
            Vector3Collection normals = new();
            Vector2Collection textureCoordinates = new();

            int vertexCount = checked(_meshWidth * _meshHeight);

            positions.Capacity = vertexCount;
            normals.Capacity = vertexCount;
            textureCoordinates.Capacity = vertexCount;

            float halfWidth = (_width - 1) / 2.0f;
            float halfHeight = (_height - 1) / 2.0f;

            for (int x = 0; x < _meshWidth; x++)
            {
                float u = _meshWidth <= 1
                    ? 0.0f
                    : (float)x / (_meshWidth - 1);

                int sourceX = GetSourceX(x);

                for (int y = 0; y < _meshHeight; y++)
                {
                    float v = _meshHeight <= 1
                        ? 0.0f
                        : (float)y / (_meshHeight - 1);

                    int sourceY = GetSourceY(y);

                    float elevation = _elevationMap![sourceX, sourceY];

                    float normalizedElevation = NormalizeElevation(
                        elevation,
                        _minimumElevation,
                        _maximumElevation);

                    // Use source coordinates for the physical position.
                    // This preserves the actual map dimensions even when
                    // the 3D mesh has been downsampled.
                    float px = sourceX - halfWidth;
                    float py = normalizedElevation * _elevationScale;
                    float pz = sourceY - halfHeight;

                    positions.Add(new Vector3(px, py, pz));

                    // Calculated after all positions are available.
                    normals.Add(Vector3.UnitY);

                    textureCoordinates.Add(new Vector2(u, v));
                }
            }

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
                checked((_meshWidth - 1) * (_meshHeight - 1) * 2);

            indices.Capacity = checked(triangleCount * 3);

            for (int x = 0; x < _meshWidth - 1; x++)
            {
                for (int y = 0; y < _meshHeight - 1; y++)
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
            for (int x = 0; x < _meshWidth; x++)
            {
                for (int y = 0; y < _meshHeight; y++)
                {
                    normals[GetVertexIndex(x, y)] =
                        CalculateNormal(x, y);
                }
            }
        }

        private Vector3 CalculateNormal(int meshX, int meshY)
        {
            int leftMeshX = Math.Max(0, meshX - 1);
            int rightMeshX = Math.Min(_meshWidth - 1, meshX + 1);

            int downMeshY = Math.Max(0, meshY - 1);
            int upMeshY = Math.Min(_meshHeight - 1, meshY + 1);

            int leftSourceX = GetSourceX(leftMeshX);
            int rightSourceX = GetSourceX(rightMeshX);

            int downSourceY = GetSourceY(downMeshY);
            int upSourceY = GetSourceY(upMeshY);

            float leftElevation =
                GetTerrainHeight(leftSourceX, GetSourceY(meshY));

            float rightElevation =
                GetTerrainHeight(rightSourceX, GetSourceY(meshY));

            float downElevation =
                GetTerrainHeight(GetSourceX(meshX), downSourceY);

            float upElevation =
                GetTerrainHeight(GetSourceX(meshX), upSourceY);

            float dx;

            if (rightSourceX != leftSourceX)
            {
                dx =
                    (rightElevation - leftElevation) *
                    _elevationScale /
                    (rightSourceX - leftSourceX);
            }
            else
            {
                dx = 0.0f;
            }

            float dz;

            if (upSourceY != downSourceY)
            {
                dz =
                    (upElevation - downElevation) *
                    _elevationScale /
                    (upSourceY - downSourceY);
            }
            else
            {
                dz = 0.0f;
            }

            Vector3 normal = new(
                -dx,
                1.0f,
                -dz);

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

        private void SetTexturePixel(int meshX, int meshY)
        {
            if (_texturePixels == null ||
                _elevationMap == null ||
                _heightMap == null)
                return;

            int sourceX = GetSourceX(meshX);
            int sourceY = GetSourceY(meshY);

            SKColor color;

            if (_isInsideLandform != null &&
                !_isInsideLandform(sourceX, sourceY))
            {
                color = SKColors.Black;
            }
            else
            {
                float elevation = _elevationMap[sourceX, sourceY];
                color = GetHeightMapColor(elevation);
            }

            int index = checked(
                (meshY * _meshWidth + meshX) * 4);

            _texturePixels[index + 0] = color.Red;
            _texturePixels[index + 1] = color.Green;
            _texturePixels[index + 2] = color.Blue;
            _texturePixels[index + 3] = color.Alpha;
        }

        private int GetVertexIndex(int x, int y)
        {
            return x * _meshHeight + y;
        }

    }

}
