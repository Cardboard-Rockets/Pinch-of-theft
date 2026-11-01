using System.Collections.Generic;
using System.Linq;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.CCTV
{
    public sealed class CameraMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform[] groupContainers;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Color activeCameraColor = new(0.15f, 0.7f, 0.5f);
        [SerializeField] private GameObject crosshair;
        [SerializeField] private Text cameraLabel;
        [SerializeField, Min(1)] private int gridColumns = 4;
        [SerializeField, Min(0)] private float gridSpacing = 16f;
        private readonly List<Button> pool = new();
        private CameraManager manager;
        private RectTransform viewport;
        private GridLayoutGroup grid;
        private CameraMenuArt art;
        private float lastWidth = -1;
        public bool IsOpen => panel != null && panel.activeSelf;

        // Сохраняет ссылки, переданные сборщиком интерфейса.
        public void Configure(GameObject p, Transform[] containers, Button template, GameObject aim = null, Text label = null)
        { panel = p; groupContainers = containers; buttonTemplate = template; crosshair = aim; cameraLabel = label; }

        // Передаёт название активной камеры основному интерфейсу.
        public void SetCameraLabel(string text)
        { if (GameContext.Instance != null) GameContext.Instance.Hud.SetCameraLabel(text); }

        // Создаёт оформление меню и подписывается на изменения доступности камер.
        public void Initialize(CameraManager cameraManager)
        {
            manager = cameraManager;
            panel.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
            BuildMenu();
            manager.ActiveCameraChanged += Changed;
            foreach (var camera in manager.Cameras)
                if (camera != null) camera.AccessibilityChanged += Refresh;
        }

        // Создаёт прямоугольник интерфейса с указанными якорями.
        private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        // Помещает сетку камер внутрь фонового изображения без изменения пропорций.
        private void BuildMenu()
        {
            art = Resources.Load<CameraMenuArt>("CameraMenuArt");
            if (art == null || art.background == null || art.cameraButton == null)
            {
                Debug.LogError("CameraMenuArt sprites are missing.", this);
                return;
            }
            foreach (Transform child in panel.transform) child.gameObject.SetActive(false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            var shade = panel.GetComponent<Image>();
            if (shade != null) { shade.sprite = null; shade.color = new Color(0, 0, 0, 0.8f); }
            var inset = CreateRect("Camera Menu Bounds", panel.transform, new Vector2(.04f, .04f), new Vector2(.96f, .96f));
            var frame = CreateRect("Camera Menu Artwork", inset, Vector2.zero, Vector2.one);
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = art.background;
            image.preserveAspect = true;
            var aspect = frame.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = art.background.rect.width / art.background.rect.height;
            viewport = CreateRect("Camera Grid Viewport", frame, new Vector2(.07f, .055f), new Vector2(.93f, .895f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect("Camera Grid", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, gridColumns);
            grid.spacing = Vector2.one * gridSpacing;
            grid.childAlignment = TextAnchor.UpperCenter;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35;
        }

        // Обновляет выделение при переключении активной камеры.
        private void Changed(CCTVCamera camera) { if (IsOpen) RebuildButtons(); }

        // Обновляет набор кнопок после открытия или блокировки камер.
        private void Refresh() { if (IsOpen) RebuildButtons(); }

        // Открывает меню по Tab и сохраняет исходную блокировку управления и курсора.
        public void SetOpen(bool open)
        {
            var context = GameContext.Instance;
            if (context == null || panel == null) return;
            if (open && context.InputLock.HasOtherOwner(this)) return;
            if (open) { context.InputLock.Acquire(this); context.CursorController.Unlock(this); context.Hud.Hide(this); }
            else { context.InputLock.Release(this); context.CursorController.Lock(this); context.Hud.Show(this); }
            if (open == IsOpen) return;
            panel.SetActive(open);
            if (open)
            {
                RebuildButtons();
                Canvas.ForceUpdateCanvases();
                ResizeGrid();
            }
        }

        // Заполняет сетку видимыми камерами в порядке их номеров.
        private void RebuildButtons()
        {
            if (grid == null) return;
            int used = 0;
            foreach (var camera in manager.Cameras.Where(c => c != null && c.IsVisibleInMenu && c.isActiveAndEnabled)
                         .OrderBy(c => c.Group).ThenBy(c => c.Slot))
            {
                if (used == pool.Count)
                {
                    var created = Instantiate(buttonTemplate, grid.transform);
                    created.image.sprite = art.cameraButton;
                    created.image.type = Image.Type.Simple;
                    created.image.preserveAspect = true;
                    var text = created.GetComponentInChildren<Text>(true);
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = Color.white;
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 12;
                    text.resizeTextMaxSize = 32;
                    text.raycastTarget = false;
                    text.rectTransform.anchorMin = new Vector2(.18f, .18f);
                    text.rectTransform.anchorMax = new Vector2(.82f, .82f);
                    text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                    pool.Add(created);
                }
                var button = pool[used++];
                button.name = "Select_" + camera.CameraName;
                button.GetComponentInChildren<Text>(true).text = camera.CameraName;
                button.interactable = camera.isAccessible;
                button.image.color = !camera.isAccessible ? Color.gray : camera == manager.ActiveCamera ? activeCameraColor : Color.white;
                button.onClick.RemoveAllListeners();
                var selected = camera;
                button.onClick.AddListener(() => manager.SelectCamera(selected));
                button.gameObject.SetActive(true);
            }
            for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
            ResizeGrid();
        }

        // Сохраняет квадратную форму кнопок и рассчитывает высоту прокручиваемой сетки.
        private void ResizeGrid()
        {
            if (viewport == null || grid == null) return;
            lastWidth = viewport.rect.width;
            int columns = Mathf.Max(1, gridColumns);
            grid.constraintCount = columns;
            float size = Mathf.Max(1, (lastWidth - gridSpacing * (columns - 1)) / columns);
            grid.cellSize = Vector2.one * size;
            int count = pool.Count(button => button.gameObject.activeSelf);
            int rows = Mathf.CeilToInt((float)count / columns);
            float height = rows * size + Mathf.Max(0, rows - 1) * gridSpacing;
            ((RectTransform)grid.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewport.rect.height, height));
        }

        // Подстраивает сетку при изменении размера окна игры.
        private void LateUpdate()
        {
            if (IsOpen && viewport != null && !Mathf.Approximately(lastWidth, viewport.rect.width)) ResizeGrid();
        }

        // Освобождает управление при отключении меню.
        private void OnDisable() { if (IsOpen) SetOpen(false); }

        // Удаляет подписки перед уничтожением интерфейса.
        private void OnDestroy()
        {
            if (manager == null) return;
            manager.ActiveCameraChanged -= Changed;
            foreach (var camera in manager.Cameras)
                if (camera != null) camera.AccessibilityChanged -= Refresh;
        }
    }
}
