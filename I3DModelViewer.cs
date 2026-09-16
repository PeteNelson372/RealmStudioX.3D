using RealmStudioShapeRenderingLib;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RealmStudioX._3D
{
    public interface I3DModelViewer
    {
        bool IsModelLoaded { get; }
        void LoadModel(string modelPath);
        void FitModel(Rect3D? bounds);
        void ResetCamera();
        void SetCameraView(ModelViewDirection viewDirection);
        void ShowViewCube(bool show);
        void ShowCoordinateSystem(bool show);
        void ShowGrid(bool show);
        void ShowBoundingBox(bool show);
        void ShowWireframe(bool show);
        void SetCameraProjection(CameraProjection projection);
        void SetUpDirection(ModelUpDirection upDirection);
    }
}
