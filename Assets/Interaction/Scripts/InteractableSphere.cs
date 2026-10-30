using PinchOfTheft.CCTV;
using UnityEngine;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [SerializeField] private int unlockGroup;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        public bool IsAvailable { get; private set; } = true;

        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress);
            prompt.SetHighlighted(highlighted);
        }

        public void Complete()
        {
            if (!IsAvailable || cameraManager == null) return;
            cameraManager.UnlockGroup(unlockGroup);
            IsAvailable = false;
            if (prompt != null) prompt.Hide();
        }
    }
}
