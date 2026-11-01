using System.Collections.Generic;
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Core
{
    public sealed class Hud : MonoBehaviour
    {
        [SerializeField, Tooltip("Прицел в центре экрана")] private GameObject crosshair;
        [SerializeField, Tooltip("Номер активной камеры")] private Text cameraLabel;
        private readonly HashSet<object> owners = new();
        private CameraManager manager;
        private void Start() { if (!GameContext.Require(this)) return; manager = GameContext.Instance.CameraManager; manager.ActiveCameraChanged += Changed; Changed(manager.ActiveCamera); }
        private void OnDestroy() { if (manager != null) manager.ActiveCameraChanged -= Changed; }
        private void Changed(CCTVCamera camera) => SetCameraLabel(camera != null ? camera.DisplayLabel : "NO SIGNAL");
        public void SetCameraLabel(string text) { if (cameraLabel != null && cameraLabel.text != text) cameraLabel.text = text; }
        public void Hide(object owner) { owners.Add(owner); Apply(); }
        public void Show(object owner) { owners.Remove(owner); Apply(); }
        private void Apply() { if (crosshair != null) crosshair.SetActive(owners.Count == 0); if (cameraLabel != null) cameraLabel.gameObject.SetActive(owners.Count == 0); }
    }
}
