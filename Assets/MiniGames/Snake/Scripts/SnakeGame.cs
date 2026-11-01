using UnityEngine;
using UnityEngine.InputSystem;

namespace Snake
{
    public sealed class SnakeGame : MonoBehaviour
    {
        [SerializeField, Min(6)] private int columns = 28;
        [SerializeField, Min(3)] private int rows = 16;
        [SerializeField, Min(0.05f)] private float stepInterval = 0.16f;
        [SerializeField] private SnakeView view;
        private SnakeBoard board;
        private float elapsed;
        private bool turnQueued;

        // Подключает созданный интерфейс и представление игрового поля.
        public void Configure(SnakeView snakeView)
        {
            view = snakeView;
        }

        // Создаёт клеточную модель и показывает начальную змейку из трёх клеток.
        private void Start()
        {
            board = new SnakeBoard(columns, rows, System.Environment.TickCount);
            view.Render(board);
#if !ENABLE_INPUT_SYSTEM
            view.ShowMessage("Включите Active Input Handling: Input System Package (New)");
            enabled = false;
#endif
        }

        // Считывает WASD и передвигает змейку с фиксированным настраиваемым интервалом.
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (board.State != SnakeState.Playing)
            {
                if (board.State == SnakeState.Lost && ((Keyboard.current?.spaceKey.wasPressedThisFrame == true) || (Mouse.current?.leftButton.wasPressedThisFrame == true))) Restart();
                return;
            }
            if (!turnQueued)
            {
                if ((Keyboard.current?.wKey.wasPressedThisFrame == true)) turnQueued = board.Turn(0, -1);
                else if ((Keyboard.current?.sKey.wasPressedThisFrame == true)) turnQueued = board.Turn(0, 1);
                else if ((Keyboard.current?.aKey.wasPressedThisFrame == true)) turnQueued = board.Turn(-1, 0);
                else if ((Keyboard.current?.dKey.wasPressedThisFrame == true)) turnQueued = board.Turn(1, 0);
            }
            elapsed += Time.deltaTime;
            while (elapsed >= stepInterval && board.State == SnakeState.Playing)
            {
                elapsed -= stepInterval;
                board.Step();
                turnQueued = false;
                view.Render(board);
            }
#endif
        }

        // Сбрасывает длину, пароль, позиции символов и таймер после поражения.
        private void Restart()
        {
            board.Reset();
            elapsed = 0f;
            turnQueued = false;
            view.Render(board);
        }
    }
}
