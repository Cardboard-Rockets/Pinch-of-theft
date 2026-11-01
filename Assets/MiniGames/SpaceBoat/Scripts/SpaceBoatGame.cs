using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceBoat
{
    public enum GameState { Playing, Lost, Won }

    public sealed class SpaceBoatGame : MonoBehaviour
    {
        [Header("Строй противников")]
        [SerializeField, Min(1)] private int rows = 3;
        [SerializeField, Min(1)] private int columns = 7;
        [SerializeField, Min(0.1f)] private float enemySpeed = 0.55f;
        [SerializeField, Min(0.1f)] private float enemyWidth = 1.15f;
        [SerializeField, Min(0f)] private float horizontalGap = 1.3f;
        [SerializeField, Min(0f)] private float verticalGap = 0.6f;
        [SerializeField, Min(0f)] private float spawnOffset = 0.5f;
        [Header("Корабль и стрельба")]
        [SerializeField, Min(0.1f)] private float playerWidth = 1.1f;
        [SerializeField] private float playerY = -6.8f;
        [SerializeField, Min(0.05f)] private float fireInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 18f;
        [SerializeField, Min(0.01f)] private float projectileWidth = 0.1f;
        [Header("Границы игрового поля")]
        [SerializeField] private float leftEdge = -15.8f;
        [SerializeField] private float rightEdge = 15.8f;
        [SerializeField] private float topEdge = 8.8f;
        [SerializeField] private float bottomEdge = -7.7f;
        private readonly List<Invader> enemies = new List<Invader>();
        private readonly List<LaserProjectile> projectiles = new List<LaserProjectile>();
        private SpaceBoatScene scene;
        private ShipController player;
        private Text score;
        private Text message;
        private int destroyed;
        private int total;
        private bool restartArmed;
        public GameState State { get; private set; }
        public event System.Action<GameState> Finished;
        public float EnemySpeed => enemySpeed;
        public float BottomEdge => bottomEdge;
        public Vector4 VisibleField => new Vector4(leftEdge, bottomEdge, rightEdge, topEdge);
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLimit => Mathf.Max(topEdge, scene.CameraTop) + spawnOffset
            + rows * (enemyWidth * scene.EnemyAspectHeight + verticalGap) + 2f;
        public ShipController Player => player;

        // Подключает локальный сборщик сцены и текстовые поля интерфейса.
        public void Configure(SpaceBoatScene owner, Text scoreText, Text resultText)
        {
            scene = owner;
            score = scoreText;
            message = resultText;
        }

        // Создаёт единственный строй противников и корабль игрока.
        private void Start()
        {
            Restart();
#if !ENABLE_INPUT_SYSTEM
            State = GameState.Lost;
            message.text = "Enable Active Input Handling: Input System Package (New)";
#endif
        }

        // Принимает перезапуск после отпускания кнопки стрельбы при поражении.
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (State != GameState.Lost) return;
            if (!(Mouse.current?.leftButton.isPressed == true) && !(Keyboard.current?.spaceKey.isPressed == true)) restartArmed = true;
            if (restartArmed && ((Mouse.current?.leftButton.wasPressedThisFrame == true) || (Keyboard.current?.spaceKey.wasPressedThisFrame == true))) Restart();
#endif
        }

        // Очищает прошлый забег и размещает новый строй целиком за верхней границей экрана.
        public void Restart()
        {
            foreach (Invader enemy in enemies) Remove(enemy.gameObject);
            foreach (LaserProjectile shot in projectiles) if (shot != null) Remove(shot.gameObject);
            if (player != null) Remove(player.gameObject);
            enemies.Clear();
            projectiles.Clear();
            destroyed = 0;
            total = Mathf.Max(1, rows) * Mathf.Max(1, columns);
            restartArmed = false;
            State = GameState.Playing;
            message.text = "";
            player = scene.CreatePlayer(playerWidth, playerY, leftEdge, rightEdge, fireInterval, this);
            float height = scene.EnemyAspectHeight * enemyWidth;
            int count = Mathf.Max(1, columns);
            float available = rightEdge - leftEdge - enemyWidth;
            float spacing = count > 1 ? Mathf.Min(enemyWidth + horizontalGap, available / (count - 1)) : 0f;
            float firstX = (leftEdge + rightEdge) * 0.5f - (count - 1) * spacing * 0.5f;
            float firstY = Mathf.Max(topEdge, scene.CameraTop) + spawnOffset + height * 0.5f;
            for (int row = 0; row < Mathf.Max(1, rows); row++)
                for (int column = 0; column < count; column++)
                    enemies.Add(scene.CreateEnemy(enemyWidth,
                        new Vector2(firstX + column * spacing, firstY + row * (height + verticalGap)), this));
            RefreshScore();
        }

        // Создаёт лазер над кораблём с заданной шириной и сохранением пропорций текстуры.
        public void Shoot(Vector2 origin)
        {
            if (State != GameState.Playing) return;
            projectiles.RemoveAll(IsDestroyed);
            projectiles.Add(scene.CreateProjectile(projectileWidth, origin, this));
        }

        // Определяет удалённые снаряды для очистки списка.
        private static bool IsDestroyed(LaserProjectile shot)
        {
            return shot == null;
        }

        // Находит ближайшего врага на всём пути снаряда между двумя физическими шагами.
        public bool HitAlongPath(Vector2 from, Vector2 to, Vector2 size)
        {
            Invader closest = null;
            float closestY = float.PositiveInfinity;
            foreach (Invader enemy in enemies)
            {
                Bounds bounds = enemy.Bounds;
                if (from.x + size.x * 0.5f < bounds.min.x || from.x - size.x * 0.5f > bounds.max.x) continue;
                if (to.y + size.y * 0.5f < bounds.min.y || from.y - size.y * 0.5f > bounds.max.y) continue;
                if (bounds.min.y >= closestY) continue;
                closest = enemy;
                closestY = bounds.min.y;
            }
            if (closest == null) return false;
            enemies.Remove(closest);
            Remove(closest.gameObject);
            destroyed++;
            RefreshScore();
            if (enemies.Count == 0)
            {
                State = GameState.Won;
                Finished?.Invoke(State);
            }
            return true;
        }

        // Завершает игру при контакте с кораблём или проходе противника к нижней границе.
        public void Lose()
        {
            if (State != GameState.Playing) return;
            State = GameState.Lost;
            restartArmed = false;
            message.text = "Game over\nSpace or click to retry";
            Finished?.Invoke(State);
        }

        // Обновляет количество уничтоженных противников.
        private void RefreshScore()
        {
            score.text = destroyed + " / " + total;
        }

        // Немедленно выключает объект и безопасно удаляет его в конце кадра.
        private static void Remove(GameObject item)
        {
            item.SetActive(false);
            Destroy(item);
        }
    }
}
