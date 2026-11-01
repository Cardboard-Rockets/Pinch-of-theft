using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Interaction
{
    public sealed class KeyPrompt : MonoBehaviour
    {
        [SerializeField] private PinchOfTheft.CCTV.CameraManager cameraManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fill;
        private bool highlighted;
        private float progress;
        // Скрывает старую графику подсказки, сохраняя данные заполнения.
        private void Awake()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
            SetHighlighted(false);
        }
        // Переключает иконку при наведении.
        public void SetHighlighted(bool value)
        {
            highlighted = value;
            if (canvasGroup != null) canvasGroup.alpha = value ? 1f : 0f;
            enabled = value;
            if (!value) InteractionIconOverlay.Hide(this);
        }
        // Обновляет заполнение при удержании клавиши.
        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            if (fill != null) fill.fillAmount = progress;
        }
        // Привязывает экранную подсказку к объекту.
        private void LateUpdate()
        {
            if (highlighted) InteractionIconOverlay.Show(this, transform, InteractionIconOverlay.Kind.Interact, progress);
        }
        // Скрывает подсказку завершённого действия.
        public void Hide()
        {
            SetHighlighted(false);
            gameObject.SetActive(false);
        }
        // Убирает подсказку при отключении объекта.
        private void OnDisable()
        {
            InteractionIconOverlay.Hide(this);
        }
    }
}
