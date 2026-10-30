using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class KeyPrompt : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fill;
        private void Awake() => SetHighlighted(false);

        public void SetHighlighted(bool value)
        {
            if (canvasGroup != null) canvasGroup.alpha = value ? 1f : 0f;
        }
        public void SetProgress(float value)
        {
            if (fill != null) fill.fillAmount = Mathf.Clamp01(value);
        }
        public void Hide() => gameObject.SetActive(false);
        private void LateUpdate()
        {
            if (cameraManager != null && cameraManager.ActiveCamera != null)
                transform.rotation = cameraManager.ActiveCamera.transform.rotation;
        }
    }
}
