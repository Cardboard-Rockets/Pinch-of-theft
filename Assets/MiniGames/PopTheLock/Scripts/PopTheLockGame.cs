using UnityEngine;
using UnityEngine.InputSystem;

namespace PopTheLock
{
    public sealed class PopTheLockGame : MonoBehaviour
    {
        [Header("Замки и ключ")]
        [SerializeField, Range(1, 24)] private int lockCount = 8;
        [SerializeField, Min(1)] private int startingLives = 3;
        [SerializeField, Min(1f)] private float angularSpeed = 100f;
        [SerializeField] private bool clockwise = true;
        [SerializeField, Range(1f, 45f)] private float hitAngle = 10f;
        [SerializeField] private float firstLockAngle = 90f;
        [SerializeField] private float initialKeyAngle = 67.5f;
        [Header("Анимация успешного попадания")]
        [SerializeField, Min(0.01f)] private float pullDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float holdDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float returnDuration = 0.22f;
        [Header("Анимация промаха")]
        [SerializeField, Min(0.01f)] private float missDuration = 0.32f;
        [SerializeField, Range(0f, 1f)] private float missDepth = 0.25f;
        private LockRound round;
        private LockView view;
        private bool restartArmed;
        public LockRound Round => round;

        // Подключает локальное представление сервера, замков и ключа.
        public void Configure(LockView sceneView)
        {
            view = sceneView;
        }

        // Начинает раунд с восемью закрытыми замками и проверяет доступность Input System.
        private void Start()
        {
            Restart();
#if !ENABLE_INPUT_SYSTEM
            view.ShowMessage("Enable Active Input Handling: Input System Package (New)");
            enabled = false;
#endif
        }

        // Обрабатывает одиночное нажатие и обновляет вращение ключа до завершения раунда.
        private void Update()
        {
            if (round.Won) return;
#if ENABLE_INPUT_SYSTEM
            if (round.Lost)
            {
                if (!(Mouse.current?.leftButton.isPressed == true) && !(Keyboard.current?.spaceKey.isPressed == true)) restartArmed = true;
                if (restartArmed && ((Mouse.current?.leftButton.wasPressedThisFrame == true) || (Keyboard.current?.spaceKey.wasPressedThisFrame == true))) Restart();
                return;
            }
            if ((Mouse.current?.leftButton.wasPressedThisFrame == true) || (Keyboard.current?.spaceKey.wasPressedThisFrame == true)) round.Press();
#endif
            round.Advance(Time.deltaTime);
            view.Render(round);
        }

        // Начинает новый раунд с закрытыми замками, полным запасом жизней и исходным направлением.
        public void Restart()
        {
            round = new LockRound(lockCount, clockwise ? -angularSpeed : angularSpeed, hitAngle,
                initialKeyAngle, firstLockAngle, pullDuration, holdDuration, returnDuration, missDuration, missDepth,
                startingLives);
            restartArmed = false;
            view.Build(round);
        }
    }
}
