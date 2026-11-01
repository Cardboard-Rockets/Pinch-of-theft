using UnityEngine;
using UnityEngine.InputSystem;

namespace Runner
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private RunnerGameManager game;
        [SerializeField] private BoxCollider2D ground;
        [SerializeField, Min(0.1f)] private float jumpSpeed = 10f;
        [SerializeField, Min(0.1f)] private float gravity = 28f;
        [SerializeField, Min(0.01f)] private float maxHoldTime = 0.22f;
        [SerializeField, Range(0.05f, 1f)] private float heldGravityMultiplier = 0.3f;
        [SerializeField, Min(0.001f)] private float groundProbeDistance = 0.06f;
        private Rigidbody2D body;
        private BoxCollider2D box;
        private Vector2 startPosition;
        private float holdTime;
        private bool running;
        private bool jumpQueued;
        private bool jumpHeld;
        private bool jumpActive;

        // Назначает локальные ссылки на менеджер и поверхность приземления.
        public void Configure(RunnerGameManager manager, BoxCollider2D floor)
        {
            game = manager;
            ground = floor;
        }

        // Запоминает компоненты и исходное положение игрока.
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            startPosition = body.position;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }

        // Считывает нажатие и удержание пробела или левой кнопки мыши через Input System.
        private void Update()
        {
            if (!running) return;
#if ENABLE_INPUT_SYSTEM
            if ((Keyboard.current?.spaceKey.wasPressedThisFrame == true) || (Mouse.current?.leftButton.wasPressedThisFrame == true))
                jumpQueued = IsGrounded();
            jumpHeld = (Keyboard.current?.spaceKey.isPressed == true) || (Mouse.current?.leftButton.isPressed == true);
#endif
        }

        // Применяет прыжок и локальную гравитацию с увеличением высоты при удержании.
        private void FixedUpdate()
        {
            if (!running) return;
            if (jumpQueued)
            {
                if (IsGrounded())
                {
                    body.linearVelocity = Vector2.up * jumpSpeed;
                    holdTime = 0f;
                    jumpActive = true;
                }
                jumpQueued = false;
            }
            float multiplier = 1f;
            if (jumpActive && jumpHeld && holdTime < maxHoldTime && body.linearVelocity.y > 0f)
            {
                multiplier = heldGravityMultiplier;
                holdTime += Time.fixedDeltaTime;
            }
            else jumpActive = false;
            body.AddForce(Vector2.down * (gravity * multiplier * body.mass), ForceMode2D.Force);
        }

        // Проверяет опору непосредственно под нижней гранью коллайдера игрока.
        private bool IsGrounded()
        {
            if (ground == null || body.linearVelocity.y > 0f) return false;
            Bounds feet = box.bounds;
            Bounds floor = ground.bounds;
            float gap = feet.min.y - floor.max.y;
            return feet.max.x > floor.min.x && feet.min.x < floor.max.x
                && gap >= -groundProbeDistance && gap <= groundProbeDistance;
        }

        // Завершает забег при физическом контакте с любой стеной Runner.
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (running && collision.collider.GetComponentInParent<Obstacle>() != null) game.Lose();
        }

        // Включает или останавливает физическое движение игрока.
        public void SetRunning(bool value)
        {
            running = value;
            body.linearVelocity = Vector2.zero;
            body.simulated = value;
            jumpQueued = false;
            jumpHeld = false;
            jumpActive = false;
        }

        // Возвращает игрока на землю и очищает состояние предыдущего прыжка.
        public void ResetRun()
        {
            body.position = startPosition;
            transform.position = startPosition;
            holdTime = 0f;
            SetRunning(true);
        }
    }
}
