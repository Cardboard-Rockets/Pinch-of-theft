using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runner
{
    public sealed class ScoreView : MonoBehaviour
    {
        [SerializeField] private Text score;
        [SerializeField] private Text result;
        [SerializeField] private RunnerGameManager game;

        // Назначает текстовые поля и менеджер текущей сцены.
        public void Configure(Text scoreText, Text resultText, RunnerGameManager manager)
        {
            score = scoreText;
            result = resultText;
            game = manager;
        }

        // Обновляет счёт сверху по центру экрана.
        public void ShowDistance(float distance, float target)
        {
            score.text = $"{Mathf.FloorToInt(distance)} / {target:0} KB";
        }

        // Показывает сообщение о поражении или завершении забега.
        public void ShowResult(RunnerGameManager.GameState state)
        {
            result.gameObject.SetActive(true);
            result.text = state == RunnerGameManager.GameState.Lost
                ? "Game over\nSpace or click to retry" : "";
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
            result.text = "Enable Active Input Handling: Input System Package (New)\nand restart the editor";
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
