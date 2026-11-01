using UnityEngine;

namespace SpaceBoat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class Invader : MonoBehaviour
    {
        private SpaceBoatGame game;
        private Rigidbody2D body;
        private BoxCollider2D shape;
        private SpriteRenderer visual;
        private MaterialPropertyBlock clipping;
        private static readonly int ClipEnabledId = Shader.PropertyToID("_ClipEnabled");
        private static readonly int ClipRectId = Shader.PropertyToID("_ClipRect");
        private static readonly int WorldToFieldId = Shader.PropertyToID("_WorldToField");
        public Bounds Bounds => new Bounds(body.position, shape.size);

        // Подключает противника к текущему забегу и запоминает физические компоненты.
        public void Configure(SpaceBoatGame manager)
        {
            game = manager;
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<BoxCollider2D>();
            visual = GetComponentInChildren<SpriteRenderer>();
            clipping = new MaterialPropertyBlock();
            UpdateClipping();
        }

        // Обновляет границы отсечения перед отрисовкой при перемещении поля или изменении параметров.
        private void LateUpdate()
        {
            UpdateClipping();
        }

        // Передаёт спрайту область видимости, сохраняя существующие параметры рендера.
        private void UpdateClipping()
        {
            visual.GetPropertyBlock(clipping);
            clipping.SetFloat(ClipEnabledId, 1f);
            clipping.SetVector(ClipRectId, game.VisibleField);
            clipping.SetMatrix(WorldToFieldId, game.transform.worldToLocalMatrix);
            visual.SetPropertyBlock(clipping);
        }

        // Двигает противника вниз и проверяет столкновение с кораблём и нижнюю границу.
        private void FixedUpdate()
        {
            if (game.State != GameState.Playing) return;
            Vector2 previous = body.position;
            body.position += Vector2.down * (game.EnemySpeed * Time.fixedDeltaTime);
            Bounds swept = Bounds;
            swept.Encapsulate(new Bounds(previous, shape.size));
            if (swept.Intersects(game.Player.Bounds) || Bounds.min.y <= game.BottomEdge) game.Lose();
        }
    }
}
