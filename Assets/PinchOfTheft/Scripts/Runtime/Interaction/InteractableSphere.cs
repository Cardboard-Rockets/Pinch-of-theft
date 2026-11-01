using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [Header("Действие при удержании F")]
        [SerializeField, Tooltip("Группа камер; -1 отключает разблокировку")] private int unlockGroup = -1;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField, Min(0f), Tooltip("Секунды удержания; 0 использует длительность Interactor")] private float holdDuration;
        [SerializeField] private bool singleUse = true;
        [SerializeField] private bool startAvailable = true;
        [SerializeField] private bool preventReactivation;
        public bool HasCompleted { get; private set; }
        public bool IsAvailable { get; private set; } = true;
        public float HoldDuration => holdDuration;
        private void Awake()
        {
            PinchOfTheft.Core.GameContext.Resolve(this);
            if (prompt == null) prompt = GetComponentInChildren<KeyPrompt>(true);
            SetAvailable(startAvailable);
        }
        private void Reset() => prompt = GetComponentInChildren<KeyPrompt>(true);
        private void OnValidate() { if (prompt == null) prompt = GetComponentInChildren<KeyPrompt>(true); }
        public void SetAvailable(bool value)
        {
            if (value && singleUse && preventReactivation && HasCompleted) return;
            IsAvailable = value;
            enabled = value;
            if (prompt == null) return;
            if (!value) prompt.Hide();
            else { prompt.gameObject.SetActive(true); prompt.SetProgress(0); prompt.SetHighlighted(false); }
        }
        // Разрешает повторную попытку взаимодействия после выхода из мини-игры без победы.
        public void CancelCompletion()
        {
            HasCompleted = false;
            SetAvailable(true);
        }

        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress); prompt.SetHighlighted(highlighted);
        }
        public void Complete()
        {
            if (!IsAvailable) return;
            HasCompleted = true;
            if (unlockGroup >= 0 && cameraManager != null) cameraManager.UnlockGroup(unlockGroup);
            if (singleUse) SetAvailable(false);
            onCompleted.Invoke();
        }
    }
}
