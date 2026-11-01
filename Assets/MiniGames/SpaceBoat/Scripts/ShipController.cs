using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceBoat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class ShipController : MonoBehaviour
    {
        private SpaceBoatGame game;
        private Camera worldCamera;
        private Rigidbody2D body;
        private BoxCollider2D shape;
        private float left;
        private float right;
        private float interval;
        private float cooldown;
        private float targetX;
        private bool firing;
        public Bounds Bounds => new Bounds(transform.position, shape.size);

        // Назначает камеру, границы перемещения и период стрельбы.
        public void Configure(SpaceBoatGame manager, Camera camera, float minimum, float maximum, float fireInterval)
        {
            game = manager;
            worldCamera = camera;
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<BoxCollider2D>();
            left = minimum + shape.size.x * 0.5f;
            right = maximum - shape.size.x * 0.5f;
            interval = fireInterval;
            targetX = body.position.x;
        }

        // Считывает горизонтальное положение мыши и удержание левой кнопки через Input System.
        private void Update()
        {
            if (game.State != GameState.Playing) return;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null || !Application.isFocused)
            {
                firing = false;
                return;
            }
            Vector3 cursor = Mouse.current.position.ReadValue();
            cursor.z = transform.position.z - worldCamera.transform.position.z;
            targetX = Mathf.Clamp(worldCamera.ScreenToWorldPoint(cursor).x, left, right);
            firing = (Mouse.current?.leftButton.isPressed == true) && Application.isFocused;
#endif
        }

        // Передвигает корабль только по горизонтали и выпускает снаряды при удержании кнопки.
        private void FixedUpdate()
        {
            if (game.State != GameState.Playing) return;
            body.position = new Vector2(targetX, body.position.y);
            cooldown = Mathf.Max(0f, cooldown - Time.fixedDeltaTime);
            if (!firing || cooldown > 0f) return;
            cooldown = interval;
            game.Shoot(body.position + Vector2.up * (shape.size.y * 0.5f));
        }
    }
}
