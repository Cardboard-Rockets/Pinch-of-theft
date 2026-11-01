using System.Collections;
using System.Collections.Generic;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using PopTheLock;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MiniGames.Integration
{
    public sealed class PopTheLockLaptop : MonoBehaviour
    {
        [SerializeField] private BoardController interaction;
        [SerializeField] private UnityEngine.Events.UnityEvent onWon = new();
        [SerializeField] private string popTheLockScene = "Assets/MiniGames/PopTheLock/Scenes/PopTheLock.unity";
        [SerializeField] private string nextScene = "Assets/Scenes/House 1.unity";
        private GameContext context;
        private PopTheLockGame game;
        private Scene loaded;
        private bool busy;
        private bool closing;
        private readonly List<Renderer> hiddenRenderers = new();
        private readonly List<Behaviour> hiddenViews = new();

        // Запускает PopTheLock после завершения удержания клавиши на ноутбуке.
        public void BeginPopTheLock()
        {
            if (busy) return;
            context = GameContext.Instance;
            if (context == null || !Application.CanStreamedLevelBeLoaded(popTheLockScene) || !Application.CanStreamedLevelBeLoaded(nextScene))
            {
                Debug.LogError("PopTheLock or next House scene is unavailable.", this);
                interaction.RetryInteraction();
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
            StartCoroutine(LoadPopTheLock());
        }

        // Загружает отдельную сцену и подписывается на результат игры.
        private IEnumerator LoadPopTheLock()
        {
            yield return SceneManager.LoadSceneAsync(popTheLockScene, LoadSceneMode.Additive);
            loaded = SceneManager.GetSceneByPath(popTheLockScene);
            foreach (var root in loaded.GetRootGameObjects())
            {
                game = root.GetComponentInChildren<PopTheLockGame>(true);
                if (game != null) break;
            }
            if (game == null) { StartCoroutine(ClosePopTheLock(false)); yield break; }
            game.Finished += Finished;
        }

        // Возвращает пользователя в меню камер при нажатии Escape.
        private void Update()
        {
            if (busy && !closing && loaded.IsValid() && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                StartCoroutine(ClosePopTheLock(false));
        }

        // Завершает мини-игру только при победе и оставляет перезапуск после поражения.
        private void Finished(bool won)
        {
            if (won && !closing) StartCoroutine(ClosePopTheLock(true));
        }

        // После победы запускает следующий дом, а при выходе восстанавливает текущую сцену.
        private IEnumerator ClosePopTheLock(bool won)
        {
            closing = true;
            if (game != null) game.Finished -= Finished;
            if (loaded.IsValid() && loaded.isLoaded) yield return SceneManager.UnloadSceneAsync(loaded);
            Restore();
            if (won)
            {
                onWon.Invoke();
                yield return SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
                yield break;
            }
            interaction.RetryInteraction();
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
