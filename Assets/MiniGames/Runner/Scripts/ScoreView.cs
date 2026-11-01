using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runner
{
    public sealed class ScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text score;
        [SerializeField] private TMP_Text result;
        [SerializeField] private RunnerGameManager game;

        // Назначает текстовые поля и менеджер текущей сцены.
        public void Configure(TMP_Text scoreText, TMP_Text resultText, RunnerGameManager manager)
        {
            score = scoreText;
            result = resultText;
            game = manager;
        }

        // Обновляет счёт сверху по центру экрана.
        public void ShowDistance(float distance, float target)
        {
            score.text = $"{Mathf.FloorToInt(distance)} / {target:0} Кб";
        }

        // Показывает сообщение о поражении или завершении забега.
        public void ShowResult(RunnerGameManager.GameState state)
        {
            result.gameObject.SetActive(true);
            result.text = state == RunnerGameManager.GameState.Lost
                ? "Поражение\nПробел или клик — начать заново" : "";
        }

        // Скрывает сообщение при начале нового забега.
        public void HideResult()
        {
            result.gameObject.SetActive(false);
        }

        // Объясняет необходимую ручную настройку Input System.
        public void ShowConfigurationError()
        {
            result.gameObject.SetActive(true);
            result.text = "Включите Active Input Handling: Input System Package (New)\nи перезапустите редактор";
        }

        // Принимает команду перезапуска только после поражения.
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (game.State == RunnerGameManager.GameState.Lost
                && ((Keyboard.current?.spaceKey.wasPressedThisFrame == true) || (Mouse.current?.leftButton.wasPressedThisFrame == true))) game.Restart();
#endif
        }
    }
}
