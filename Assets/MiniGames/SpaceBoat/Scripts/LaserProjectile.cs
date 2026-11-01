using UnityEngine;

namespace SpaceBoat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class LaserProjectile : MonoBehaviour
    {
        private SpaceBoatGame game;
        private Rigidbody2D body;
        private Vector2 size;

        // Подключает снаряд к менеджеру и запоминает размер для проверки всего пути полёта.
        public void Configure(SpaceBoatGame manager)
        {
            game = manager;
            body = GetComponent<Rigidbody2D>();
            size = GetComponent<BoxCollider2D>().size;
        }

        // Перемещает лазер вверх без пропуска врагов между шагами и удаляет его после попадания.
        private void FixedUpdate()
        {
            if (game.State != GameState.Playing) return;
            Vector2 next = body.position + Vector2.up * (game.ProjectileSpeed * Time.fixedDeltaTime);
            if (game.HitAlongPath(body.position, next, size) || next.y > game.ProjectileLimit)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            body.position = next;
        }
    }
}
