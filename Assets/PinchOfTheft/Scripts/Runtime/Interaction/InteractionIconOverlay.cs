using System.Collections.Generic;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(10000)]
    public sealed class InteractionIconOverlay : MonoBehaviour
    {
        public enum Kind { Interact, Move, NoPower, Locked, Unlocked }
        private struct Hint { public Transform anchor; public Kind kind; public float progress; }
        private static InteractionIconOverlay instance;
        private readonly Dictionary<Object, Hint> hints = new();
        private readonly List<Object> stale = new();
        private InteractionIcons icons;
        private Image icon;
        private Image progress;
        private RectTransform panel;
        private Canvas canvas;

        // Регистрирует подсказку объекта без изменения её исходного положения в сцене.
        public static void Show(Object owner, Transform anchor, Kind kind, float progress = 0f)
        {
            if (instance == null)
            {
                var root = new GameObject("Interaction Icon Overlay");
                instance = root.AddComponent<InteractionIconOverlay>();
                instance.Build();
            }
            instance.hints[owner] = new Hint { anchor = anchor, kind = kind, progress = progress };
        }

        // Убирает подсказку при потере наведения или завершении взаимодействия.
        public static void Hide(Object owner)
        {
            if (instance != null) instance.hints.Remove(owner);
        }

        // Создаёт экранный интерфейс поверх геометрии и остальных игровых подсказок.
        private void Build()
        {
            icons = Resources.Load<InteractionIcons>("InteractionIcons");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var item = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            item.transform.SetParent(transform, false);
            icon = item.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            panel = icon.rectTransform;
            panel.sizeDelta = new Vector2(80f, 80f);
            var bar = new GameObject("Hold Progress", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(panel, false);
            progress = bar.GetComponent<Image>();
            progress.raycastTarget = false;
            progress.sprite = icons != null ? icons.interact : null;
            progress.preserveAspect = true;
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Vertical;
            progress.fillOrigin = 0;
            progress.color = Color.white;
            var rect = progress.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            panel.gameObject.SetActive(false);
        }

        // Показывает только актуальную подсказку перед активной камерой с сохранением пропорций и заполнения.
        private void LateUpdate()
        {
            var context = GameContext.Instance;
            if (context == null || context.InputLock.IsLocked || context.Menu.IsOpen || icons == null)
            { panel.gameObject.SetActive(false); return; }
            Hint selected = default;
            bool found = false;
            stale.Clear();
            foreach (var pair in hints)
            {
                if (pair.Key == null || pair.Value.anchor == null || (pair.Key is Behaviour owner && !owner.isActiveAndEnabled)) { stale.Add(pair.Key); continue; }
                selected = pair.Value;
                found = true;
            }
            foreach (var key in stale) hints.Remove(key);
            var camera = context.CameraManager.ActiveCamera?.View;
            if (!found || camera == null) { panel.gameObject.SetActive(false); return; }
            var point = camera.WorldToScreenPoint(selected.anchor.position);
            if (point.z <= 0f) { panel.gameObject.SetActive(false); return; }
            panel.gameObject.SetActive(true);
            icon.sprite = selected.kind switch
            {
                Kind.Move => icons.move,
                Kind.NoPower => icons.noPower,
                Kind.Locked => icons.locked,
                Kind.Unlocked => icons.unlocked,
                _ => icons.interact
            };
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, point, null, out var local);
            panel.anchoredPosition = local;
            icon.color = selected.progress > 0f ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;
            progress.fillAmount = Mathf.Clamp01(selected.progress);
            progress.gameObject.SetActive(selected.progress > 0f);
        }

        // Освобождает статическую ссылку после выгрузки сцены.
        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
