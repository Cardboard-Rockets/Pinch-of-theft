using UnityEngine;
using UnityEngine.UI;

namespace SpaceBoat
{
    [RequireComponent(typeof(SpaceBoatGame))]
    public sealed class SpaceBoatScene : MonoBehaviour
    {
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite ship;
        [SerializeField] private Sprite enemy;
        [SerializeField] private Sprite laser;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private Font font;
        private Camera worldCamera;
        private const float BackgroundWidth = 34f;
        private const float BackgroundHeight = 19.2f;
        public float EnemyAspectHeight => enemy.bounds.size.y / enemy.bounds.size.x;
        public float CameraTop => worldCamera.transform.position.y + worldCamera.orthographicSize;

        // Собирает локальную камеру, статичный фон и интерфейс мини-игры.
        private void Awake()
        {
            worldCamera = new GameObject("SpaceBoat Camera").AddComponent<Camera>();
            worldCamera.transform.SetParent(transform, false);
            worldCamera.transform.localPosition = new Vector3(0f, 0f, -10f);
            worldCamera.orthographic = true;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = new Color32(8, 24, 32, 255);
            FitCamera();
            GameObject backdrop = CreateVisual("Static Background", background, BackgroundWidth, Vector2.zero, -10, false);
            var canvas = new GameObject("SpaceBoat UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Text score = CreateText(canvas.transform, "Score", new Vector2(0.5f, 1f), new Vector2(0f, -40f));
            Text message = CreateText(canvas.transform, "Result", new Vector2(0.5f, 0.5f), Vector2.zero);
            GetComponent<SpaceBoatGame>().Configure(this, score, message);
        }

        // Создаёт корабль с неизменными пропорциями и управлением мышью.
        public ShipController CreatePlayer(float width, float y, float left, float right, float interval, SpaceBoatGame game)
        {
            GameObject item = CreateVisual("Player Ship", ship, width, new Vector2(0f, y), 3, true);
            var controller = item.AddComponent<ShipController>();
            controller.Configure(game, worldCamera, left, right, interval);
            return controller;
        }

        // Создаёт противника с локальным спрайтом и прямым движением вниз.
        public Invader CreateEnemy(float width, Vector2 position, SpaceBoatGame game)
        {
            GameObject item = CreateVisual("Invader", enemy, width, position, 2, true);
            var controller = item.AddComponent<Invader>();
            controller.Configure(game);
            return controller;
        }

        // Создаёт снаряд непосредственно над носом корабля.
        public LaserProjectile CreateProjectile(float width, Vector2 origin, SpaceBoatGame game)
        {
            float height = width * laser.bounds.size.y / laser.bounds.size.x;
            GameObject item = CreateVisual("Laser", laser, width, origin + Vector2.up * height * 0.5f, 4, true);
            var controller = item.AddComponent<LaserProjectile>();
            controller.Configure(game);
            return controller;
        }

        // Создаёт спрайт равномерным масштабом и при необходимости добавляет локальную физику 2D.
        private GameObject CreateVisual(string name, Sprite sprite, float width, Vector2 position, int order, bool physics)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = position;
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = order;
            float scale = width / sprite.bounds.size.x;
            visual.transform.localScale = new Vector3(scale, scale, 1f);
            if (physics)
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                var shape = root.AddComponent<BoxCollider2D>();
                shape.size = new Vector2(width, sprite.bounds.size.y * scale);
                shape.isTrigger = true;
            }
            return root;
        }

        // Создаёт текст uGUI с собственным шрифтом и заданным якорем.
        private Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 position)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var text = item.AddComponent<Text>();
            text.font = font;
            text.fontSize = 38;
            text.color = new Color32(224, 248, 208, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1300f, 180f);
            return text;
        }

        // Подстраивает камеру при изменении размеров окна, сохраняя пропорции всего фона.
        private void LateUpdate()
        {
            FitCamera();
        }

        // Вписывает весь фон в окно с полями вместо растягивания изображения.
        private void FitCamera()
        {
            worldCamera.orthographicSize = Mathf.Max(BackgroundHeight * 0.5f, BackgroundWidth / (2f * worldCamera.aspect));
        }
    }
}
