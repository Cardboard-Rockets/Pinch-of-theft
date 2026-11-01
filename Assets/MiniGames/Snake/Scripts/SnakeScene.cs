using UnityEngine;
using UnityEngine.UI;

namespace Snake
{
    [RequireComponent(typeof(SnakeGame), typeof(SnakeView))]
    public sealed class SnakeScene : MonoBehaviour
    {
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite body;
        [SerializeField] private Sprite closedSlot;
        [SerializeField] private Sprite[] headFrames;
        [SerializeField] private float[] headDurations;
        [SerializeField] private Sprite[] symbols;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Font font;
        private Camera worldCamera;
        private const float ArtWidth = 34f;
        private const float ArtHeight = 19.2f;

        // Собирает независимую сцену из явно назначенных ресурсов Snake.
        private void Awake()
        {
            worldCamera = new GameObject("Snake Camera").AddComponent<Camera>();
            worldCamera.transform.SetParent(transform, false);
            worldCamera.transform.localPosition = new Vector3(0f, 0f, -10f);
            worldCamera.orthographic = true;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color32(8, 24, 32, 255);
            FitCamera();
            var backdrop = new GameObject("Static Background");
            backdrop.transform.SetParent(transform, false);
            var renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sprite = background;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = -10;
            float scale = ArtWidth / background.bounds.size.x;
            backdrop.transform.localScale = new Vector3(scale, scale, 1f);
            var canvasObject = new GameObject("Snake UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(3400f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            Text result = CreateText(canvasObject.transform, "Result", new Vector2(0.36f, 0.53f), new Vector2(2200f, 300f));
            Text progress = CreateText(canvasObject.transform, "Progress", new Vector2(0.85f, 0.4f), new Vector2(700f, 300f));
            SnakeView view = GetComponent<SnakeView>();
            view.Configure(headFrames, headDurations, body, closedSlot, symbols, spriteMaterial, result, progress);
            GetComponent<SnakeGame>().Configure(view);
        }

        // Создаёт текст uGUI с локальной копией шрифта и заданным якорем.
        private Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            Text text = item.AddComponent<Text>();
            text.font = font;
            text.fontSize = 48;
            text.color = new Color32(224, 248, 208, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return text;
        }

        // Сохраняет видимость всего фона при изменении окна без растягивания изображения.
        private void LateUpdate()
        {
            FitCamera();
        }

        // Подбирает размер ортографической камеры с полями для нестандартного соотношения сторон.
        private void FitCamera()
        {
            worldCamera.orthographicSize = Mathf.Max(ArtHeight * 0.5f, ArtWidth / (2f * worldCamera.aspect));
        }
    }
}
