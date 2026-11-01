using System.Collections;
using System.Collections.Generic;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using Snake;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MiniGames.Integration
{
    public sealed class SnakeCameraUnlock : MonoBehaviour
    {
        [SerializeField] private InteractableSphere interaction;
        [SerializeField] private int unlockGroup = 1;
        [SerializeField] private string snakeScene = "Assets/MiniGames/Snake/Scenes/Snake.unity";
        private GameContext context;
        private SnakeGame game;
        private Scene loaded;
        private bool busy;
        private bool closing;
        private readonly List<Renderer> hiddenRenderers = new();
        private readonly List<Behaviour> hiddenViews = new();

        // Запускает Snake после завершения удержания клавиши на роутере.
        public void BeginSnake()
        {
            if (busy) return;
            context = GameContext.Instance;
            if (context == null || !Application.CanStreamedLevelBeLoaded(snakeScene))
            {
                Debug.LogError("Snake scene is unavailable.", this);
                interaction.SetAvailable(true);
                return;
            }
            busy = true;
            context.Menu.SetOpen(false);
            context.InputLock.Acquire(this);
            context.CursorController.Unlock(this);
            context.Hud.Hide(this);
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    if (!renderer.forceRenderingOff) { hiddenRenderers.Add(renderer); renderer.forceRenderingOff = true; }
                foreach (var view in root.GetComponentsInChildren<Behaviour>(true))
                    if ((view is Camera || view is Canvas || view is AudioListener) && view.enabled)
                    { hiddenViews.Add(view); view.enabled = false; }
            }
            StartCoroutine(LoadSnake());
        }

        // Загружает отдельную сцену и подписывается на результат игры.
        private IEnumerator LoadSnake()
        {
            yield return SceneManager.LoadSceneAsync(snakeScene, LoadSceneMode.Additive);
            loaded = SceneManager.GetSceneByPath(snakeScene);
            foreach (var root in loaded.GetRootGameObjects())
            {
                game = root.GetComponentInChildren<SnakeGame>(true);
                if (game != null) break;
            }
            if (game == null) { StartCoroutine(CloseSnake(false)); yield break; }
            game.Finished += Finished;
        }

        // Возвращает пользователя в меню камер при нажатии Escape.
        private void Update()
        {
            if (busy && !closing && loaded.IsValid() && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                StartCoroutine(CloseSnake(false));
        }

        // Завершает мини-игру только при победе и оставляет перезапуск после поражения.
        private void Finished(SnakeState state)
        {
            if (state == SnakeState.Won && !closing) StartCoroutine(CloseSnake(true));
        }

        // Выгружает Snake и восстанавливает интерфейс дома с исходной разблокировкой группы.
        private IEnumerator CloseSnake(bool won)
        {
            closing = true;
            if (game != null) game.Finished -= Finished;
            if (loaded.IsValid() && loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(loaded);
            Restore();
            if (won) context.CameraManager.UnlockGroup(unlockGroup);
            else interaction.SetAvailable(true);
            context.Menu.SetOpen(true);
            game = null;
            loaded = default;
            busy = false;
            closing = false;
        }

        // Восстанавливает ранее включённые компоненты и освобождает собственную блокировку ввода.
        private void Restore()
        {
            foreach (var renderer in hiddenRenderers) if (renderer != null) renderer.forceRenderingOff = false;
            foreach (var view in hiddenViews) if (view != null) view.enabled = true;
            hiddenRenderers.Clear();
            hiddenViews.Clear();
            if (context == null) return;
            context.InputLock.Release(this);
            context.CursorController.Lock(this);
            context.Hud.Show(this);
        }

        // Убирает подписку и блокировку при уничтожении связующего компонента.
        private void OnDestroy()
        {
            if (game != null) game.Finished -= Finished;
            Restore();
        }
    }
}
