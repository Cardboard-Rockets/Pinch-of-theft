using UnityEngine;
using UnityEngine.UI;

namespace PopTheLock
{
    [RequireComponent(typeof(PopTheLockGame), typeof(LockView))]
    public sealed class PopTheLockScene : MonoBehaviour
    {
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite server;
        [SerializeField] private Sprite closedLock;
        [SerializeField] private Sprite openLock;
        [SerializeField] private Sprite key;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Font font;
        private const float BackgroundWidth = 34f;
        private const float BackgroundHeight = 19.2f;
        private Camera worldCamera;

        // Создаёт независимую камеру, статичный фон и интерфейс из локальных ресурсов PopTheLock.
        private void Awake()
        {
            worldCamera = new GameObject("PopTheLock Camera").AddComponent<Camera>();
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
            float scale = BackgroundWidth / background.bounds.size.x;
            backdrop.transform.localScale = new Vector3(scale, scale, 1f);
            var canvas = new GameObject("PopTheLock UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Text score = CreateText(canvas.transform, "Score", new Vector2(0.5f, 1f), new Vector2(0f, -30f));
            Text hint = CreateText(canvas.transform, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 115f));
            var lifeObject = new GameObject("Lives", typeof(RectTransform));
            lifeObject.transform.SetParent(canvas.transform, false);
            var lifeRect = lifeObject.GetComponent<RectTransform>();
            lifeRect.anchorMin = lifeRect.anchorMax = Vector2.one;
            lifeRect.pivot = Vector2.one;
            lifeRect.anchoredPosition = new Vector2(-30f, -25f);
            lifeRect.sizeDelta = new Vector2(300f, 80f);
            LivesView lives = lifeObject.AddComponent<LivesView>();
            lives.Configure(key);
            LockView view = GetComponent<LockView>();
            view.Configure(server, closedLock, openLock, key, spriteMaterial, score, hint, lives);
            GetComponent<PopTheLockGame>().Configure(view);
        }

        // Создаёт текст uGUI с локальным шрифтом и заданным расположением на экране.
        private Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var text = item.AddComponent<Text>();
            text.font = font;
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(224, 248, 208, 255);
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1500f, 90f);
            return text;
        }

        // Подгоняет камеру к окну при изменении его размеров.
        private void LateUpdate()
        {
            FitCamera();
        }

        // Вписывает фон в окно с сохранением пропорций и полями по краям.
        private void FitCamera()
        {
            worldCamera.orthographicSize = Mathf.Max(BackgroundHeight * 0.5f, BackgroundWidth / (2f * worldCamera.aspect));
        }
    }
}
