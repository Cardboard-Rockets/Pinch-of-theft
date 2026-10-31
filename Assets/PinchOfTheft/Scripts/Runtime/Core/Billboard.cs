using PinchOfTheft.CCTV;
using UnityEngine;
namespace PinchOfTheft.Core
{
    public sealed class Billboard : MonoBehaviour
    {
        private CameraManager manager;
        private Transform cameraTransform;
        private void OnEnable()
        {
            if (GameContext.Instance == null) return;
            manager = GameContext.Instance.CameraManager;
            manager.ActiveCameraChanged += Changed; Changed(manager.ActiveCamera);
        }
        private void Changed(CCTVCamera camera) => cameraTransform = camera != null ? camera.transform : null;
        private void OnDisable() { if (manager != null) manager.ActiveCameraChanged -= Changed; }
        private void LateUpdate() { if (cameraTransform != null) transform.rotation = cameraTransform.rotation; }
    }
}
