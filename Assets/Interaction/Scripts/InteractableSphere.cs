using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [SerializeField] private int unlockGroup = -1;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField, Min(0f)] private float holdDuration;
        [SerializeField] private bool singleUse = true;
        [SerializeField] private bool startAvailable = true;
        public bool IsAvailable { get; private set; } = true;
        public float HoldDuration => holdDuration;
        private void Awake() => SetAvailable(startAvailable);
        public void SetAvailable(bool value)
        {
            IsAvailable = value;
            if (prompt == null) return;
            if (!value) prompt.Hide();
            else { prompt.gameObject.SetActive(true); prompt.SetProgress(0); prompt.SetHighlighted(false); }
        }
        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress); prompt.SetHighlighted(highlighted);
        }
        public void Complete()
        {
            if (!IsAvailable) return;
            if (unlockGroup >= 0 && cameraManager != null) cameraManager.UnlockGroup(unlockGroup);
            if (singleUse) SetAvailable(false);
            onCompleted.Invoke();
        }
    }
}
