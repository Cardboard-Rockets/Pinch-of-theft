using System.Collections.Generic;
using UnityEngine;

namespace Runner
{
    public sealed class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField] private RunnerGameManager game;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Obstacle[] prefabs;
        [SerializeField] private float groundY = -4.1f;
        [SerializeField, Min(0.1f)] private float interval = 2.2f;
        [SerializeField, Min(0f)] private float intervalRandomness = 0.25f;
        [SerializeField, Min(0.1f)] private float firstDelay = 1.2f;
        [SerializeField, Min(0.1f)] private float edgeMargin = 1f;
        private readonly List<Obstacle> active = new List<Obstacle>();
        private float remaining;

        // Назначает зависимости спавнера из сборщика сцены.
        public void Configure(RunnerGameManager manager, Camera camera, Obstacle[] walls, float floorY)
        {
            game = manager;
            worldCamera = camera;
            prefabs = walls;
            groundY = floorY;
        }

        // Создаёт препятствия с настраиваемой задержкой и небольшим случайным отклонением.
        private void Update()
        {
            if (game.State != RunnerGameManager.GameState.Playing) return;
            remaining -= Time.deltaTime;
            if (remaining > 0f) return;
            Spawn();
            remaining = Mathf.Max(Time.fixedDeltaTime, interval + Random.Range(-intervalRandomness, intervalRandomness));
        }

        // Размещает случайный префаб целиком за правым краем камеры.
        private void Spawn()
        {
            if (prefabs == null || prefabs.Length == 0) return;
            Obstacle prefab = prefabs[Random.Range(0, prefabs.Length)];
            BoxCollider2D shape = prefab.GetComponent<BoxCollider2D>();
            Vector3 scale = prefab.transform.localScale;
            float rightEdge = worldCamera.transform.position.x + worldCamera.orthographicSize * worldCamera.aspect;
            Vector3 position = new Vector3(rightEdge + edgeMargin + shape.size.x * Mathf.Abs(scale.x) * 0.5f,
                groundY + shape.size.y * Mathf.Abs(scale.y) * 0.5f - shape.offset.y * scale.y, 0f);
            Obstacle wall = Instantiate(prefab, position, Quaternion.identity, transform);
            wall.Initialize(game, worldCamera, edgeMargin);
            active.RemoveAll(IsDestroyed);
            active.Add(wall);
        }

        // Определяет удалённые стены при очистке списка активных объектов.
        private static bool IsDestroyed(Obstacle wall)
        {
            return wall == null;
        }

        // Убирает стены предыдущего забега и восстанавливает начальную задержку.
        public void ResetRun()
        {
            foreach (Obstacle wall in active)
            {
                if (wall == null) continue;
                wall.gameObject.SetActive(false);
                Destroy(wall.gameObject);
            }
            active.Clear();
            remaining = firstDelay;
        }
    }
}
