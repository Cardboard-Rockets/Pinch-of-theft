using UnityEngine;
using UnityEngine.UI;

namespace PopTheLock
{
    public sealed class LockView : MonoBehaviour
    {
        [Header("Расположение и размеры")]
        [SerializeField] private Vector2 center = Vector2.zero;
        [SerializeField, Min(0.1f)] private float lockRadius = 3.4f;
        [SerializeField, Min(0.1f)] private float keyRadius = 5.4f;
        [SerializeField, Min(0.1f)] private float serverWidth = 2.4f;
        [SerializeField, Min(0.1f)] private float lockWidth = 1.9f;
        [SerializeField, Min(0.1f)] private float keyWidth = 2f;
        private Sprite closedSprite;
        private Sprite openSprite;
        private Sprite serverSprite;
        private Sprite keySprite;
        private Material material;
        private Text score;
        private Text hint;
        private SpriteRenderer[] locks;
        private SpriteRenderer key;
        private SpriteRenderer server;
        private LivesView lives;

        // Назначает локальные спрайты, материал и текстовые поля интерфейса.
        public void Configure(Sprite server, Sprite closedLock, Sprite openLock, Sprite rotatingKey,
            Material spriteMaterial, Text counter, Text instructions, LivesView livesView)
        {
            serverSprite = server;
            closedSprite = closedLock;
            openSprite = openLock;
            keySprite = rotatingKey;
            material = spriteMaterial;
            score = counter;
            hint = instructions;
            lives = livesView;
        }

        // Создаёт сервер в центре, круг закрытых замков и ключ на внешней окружности.
        public void Build(LockRound round)
        {
            keyRadius = Mathf.Max(keyRadius, lockRadius + keyWidth * 0.5f);
            if (locks != null) foreach (SpriteRenderer oldLock in locks) RemoveSprite(oldLock);
            RemoveSprite(key);
            RemoveSprite(server);
            server = CreateSprite("Server", serverSprite, serverWidth, 1);
            server.transform.localPosition = center;
            locks = new SpriteRenderer[round.Count];
            for (int index = 0; index < locks.Length; index++)
            {
                locks[index] = CreateSprite("Lock " + (index + 1), closedSprite, lockWidth, 2);
                locks[index].transform.localPosition = Circle(round.LockAngle(index), lockRadius);
            }
            key = CreateSprite("Orbiting Key", keySprite, keyWidth, 3);
            hint.text = "Click or press Space to unlock";
            Render(round);
        }

        // Отображает притяжение ключа, открытые замки и число успешно открытых замков.
        public void Render(LockRound round)
        {
            for (int index = 0; index < locks.Length; index++)
                locks[index].sprite = round.IsOpen(index) ? openSprite : closedSprite;
            float radius = Mathf.Lerp(keyRadius, lockRadius, round.InwardAmount);
            key.transform.localPosition = Circle(round.KeyAngle, radius);
            key.transform.localRotation = Quaternion.Euler(0f, 0f, round.KeyAngle + 180f);
            score.text = round.OpenedCount + " / " + round.Count;
            lives.Render(round.Lives);
            if (round.Lost) hint.text = "No lives left. Click or press Space to retry";
            if (round.Won) hint.text = "";
        }

        // Убирает старый спрайт при перезапуске без оставшихся активных копий.
        private static void RemoveSprite(SpriteRenderer sprite)
        {
            if (sprite == null) return;
            sprite.gameObject.SetActive(false);
            Destroy(sprite.gameObject);
        }

        // Показывает сообщение о необходимой настройке ввода.
        public void ShowMessage(string message)
        {
            hint.text = message;
        }

        // Вычисляет точку на окружности относительно центра сервера.
        private Vector2 Circle(float angle, float radius)
        {
            float radians = angle * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        // Создаёт спрайт с равномерным масштабом и сохранением исходного соотношения сторон.
        private SpriteRenderer CreateSprite(string name, Sprite sprite, float width, int order)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            float scale = width / sprite.bounds.size.x;
            item.transform.localScale = new Vector3(scale, scale, 1f);
            return renderer;
        }
    }
}
