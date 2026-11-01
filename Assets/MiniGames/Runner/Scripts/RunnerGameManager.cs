using System;
using UnityEngine;

namespace Runner
{
    public sealed class RunnerGameManager : MonoBehaviour
    {
        public enum GameState { Playing, Lost, Won }
        [SerializeField, Min(0.1f)] private float worldSpeed = 6f;
        [SerializeField, Min(1f)] private float targetDistance = 512f;
        [SerializeField] private PlayerController player;
        [SerializeField] private ObstacleSpawner spawner;
        [SerializeField] private ScoreView scoreView;
        public GameState State { get; private set; } = GameState.Playing;
        public float Distance { get; private set; }
        public float WorldSpeed => worldSpeed;
        public event Action<GameState> Finished;

        // Подключает объекты сцены к управляющему компоненту.
        public void Configure(PlayerController runnerPlayer, ObstacleSpawner runnerSpawner, ScoreView view)
        {
            player = runnerPlayer;
            spawner = runnerSpawner;
            scoreView = view;
        }

        // Запускает независимый игровой сеанс и проверяет доступность Input System.
        private void Start()
        {
#if !ENABLE_INPUT_SYSTEM
            State = GameState.Lost;
            player.SetRunning(false);
            scoreView.ShowConfigurationError();
            Debug.LogError("Runner: включите Active Input Handling = Input System Package (New) для Input System.", this);
#else
            Restart();
#endif
        }

        // Увеличивает дистанцию по скорости мира и завершает игру на заданной цели.
        private void Update()
        {
            if (State != GameState.Playing) return;
            Distance = Mathf.Min(targetDistance, Distance + worldSpeed * Time.deltaTime);
            scoreView.ShowDistance(Distance, targetDistance);
            if (Distance >= targetDistance) Finish(GameState.Won);
        }

        // Завершает сеанс поражением при столкновении с препятствием.
        public void Lose()
        {
            Finish(GameState.Lost);
        }

        // Останавливает только объекты этой мини-игры без изменения глобального времени.
        private void Finish(GameState state)
        {
            if (State != GameState.Playing) return;
            State = state;
            player.SetRunning(false);
            scoreView.ShowResult(state);
            Finished?.Invoke(state);
        }

        // Начинает новый забег без перезагрузки сцены и изменения настроек проекта.
        public void Restart()
        {
#if ENABLE_INPUT_SYSTEM
            Distance = 0f;
            State = GameState.Playing;
            spawner.ResetRun();
            player.ResetRun();
            scoreView.ShowDistance(Distance, targetDistance);
            scoreView.HideResult();
#endif
        }
    }
}
