using UnityEngine;

namespace Runner
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class Obstacle : MonoBehaviour
    {
        private RunnerGameManager game;
        private Rigidbody2D body;
        private BoxCollider2D box;
        private Camera worldCamera;
        private float despawnMargin;
        public RunnerGameManager Game => game;

        // Сохраняет компоненты препятствия для физического перемещения.
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
        }

        // Привязывает созданную стену к текущему забегу и камере.
        public void Initialize(RunnerGameManager manager, Camera camera, float margin)
        {
            game = manager;
            worldCamera = camera;
            despawnMargin = margin;
        }

        // Двигает стену влево и удаляет её после полного выхода за экран.
        private void FixedUpdate()
        {
            if (game == null || game.State != RunnerGameManager.GameState.Playing) return;
            body.MovePosition(body.position + Vector2.left * (game.WorldSpeed * Time.fixedDeltaTime));
            float leftEdge = worldCamera.transform.position.x - worldCamera.orthographicSize * worldCamera.aspect;
            if (box.bounds.max.x < leftEdge - despawnMargin) Destroy(gameObject);
        }
    }
}
