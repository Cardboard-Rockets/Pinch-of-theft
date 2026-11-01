using UnityEngine;

namespace Runner
{
    public sealed class WallAnimation : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float[] frameDurations;
        [SerializeField] private SpriteRenderer target;
        private int frame;
        private float elapsed;
        private RunnerGameManager game;
        private const float MinimumDuration = 0.01f;

        // Назначает извлечённые кадры огня и их длительности при сборке префаба.
        public void Configure(SpriteRenderer renderer, Sprite[] sprites, float[] durations)
        {
            target = renderer;
            frames = sprites;
            frameDurations = durations;
            ShowFrame();
        }

        // Запоминает менеджер забега и показывает первый кадр огня.
        private void Start()
        {
            Obstacle wall = GetComponent<Obstacle>();
            game = wall.Game;
            ShowFrame();
        }

        // Переключает исходные кадры GIF с их задержками только во время забега.
        private void Update()
        {
            if (frames == null || frames.Length == 0 || game == null
                || game.State != RunnerGameManager.GameState.Playing) return;
            elapsed += Time.deltaTime;
            while (elapsed >= Duration())
            {
                elapsed -= Duration();
                frame = (frame + 1) % frames.Length;
                ShowFrame();
            }
        }

        // Возвращает длительность текущего кадра с защитой от нулевой задержки.
        private float Duration()
        {
            return frameDurations != null && frame < frameDurations.Length
                ? Mathf.Max(MinimumDuration, frameDurations[frame]) : MinimumDuration;
        }

        // Назначает кадр без изменения положения или коллайдера стены.
        private void ShowFrame()
        {
            if (target != null && frames != null && frames.Length > 0) target.sprite = frames[frame];
        }
    }
}
