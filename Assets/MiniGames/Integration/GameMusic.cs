using System.Collections.Generic;
using Runner;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniGames.Integration
{
    public sealed class GameMusic : MonoBehaviour
    {
        private static GameMusic instance;
        private readonly HashSet<Scene> miniGames = new();
        private MusicSettings settings;
        private AudioSource main;
        private AudioSource mini;
        private RunnerGameManager runner;
        private Scene runnerScene;
        private bool unlocked;
        private bool mainStarted;
        private bool waiting;
        private float remainingDelay;

        // Сбрасывает статическую ссылку при новом запуске игрового режима.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            instance = null;
        }

        // Создаёт один музыкальный контроллер до загрузки первой игровой сцены.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            new GameObject("Game Music").AddComponent<GameMusic>();
        }

        // Загружает клипы и сохраняет источники музыки между сценами.
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            settings = Resources.Load<MusicSettings>("GameMusicSettings");
            if (settings == null || settings.mainTrack == null || settings.miniGameTrack == null)
            {
                Debug.LogError("Game music settings or clips are missing.", this);
                enabled = false;
                return;
            }
            main = CreateSource(settings.mainTrack, settings.mainVolume, false);
            mini = CreateSource(settings.miniGameTrack, settings.miniGameVolume, true);
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        // Создаёт независимый двумерный источник без автоматического запуска.
        private AudioSource CreateSource(AudioClip clip, float volume, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            return source;
        }

        // Распознаёт мини-игру по пути сцены, ставит основную музыку на паузу и включает TinyAdventure.
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsMiniGame(scene.path)) return;
            EnsureMiniGameListener(scene);
            if (miniGames.Add(scene) && miniGames.Count == 1)
            {
                if (mainStarted && !waiting) main.Pause();
                mini.Play();
            }
            if (scene.path != "Assets/MiniGames/Runner/Scenes/Runner.unity") return;
            foreach (var root in scene.GetRootGameObjects())
            {
                runner = root.GetComponentInChildren<RunnerGameManager>(true);
                if (runner != null) break;
            }
            if (runner == null) return;
            runnerScene = scene;
            runner.Finished += RunnerFinished;
        }

        // Добавляет слушатель камере мини-игры, если сцена ещё не содержит активного слушателя.
        private static void EnsureMiniGameListener(Scene scene)
        {
            Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var listener in root.GetComponentsInChildren<AudioListener>())
                    if (listener.enabled) return;
                if (camera == null) camera = root.GetComponentInChildren<Camera>();
            }
            if (camera == null) return;
            var existing = camera.GetComponent<AudioListener>();
            if (existing != null) existing.enabled = true;
            else camera.gameObject.AddComponent<AudioListener>();
        }

        // Разрешает основную мелодию только после успешного прохождения первого Runner.
        private void RunnerFinished(RunnerGameManager.GameState state)
        {
            if (state == RunnerGameManager.GameState.Won) unlocked = true;
        }

        // После закрытия последней мини-игры продолжает основную мелодию с сохранённой позиции.
        private void SceneUnloaded(Scene scene)
        {
            if (scene == runnerScene)
            {
                if (runner != null) runner.Finished -= RunnerFinished;
                runner = null;
            }
            if (!miniGames.Remove(scene) || miniGames.Count != 0) return;
            mini.Stop();
            if (!unlocked) return;
            if (!mainStarted) StartMain();
            else if (!waiting) main.UnPause();
        }

        // Проверяет принадлежность сцены к одной из четырёх мини-игр.
        private static bool IsMiniGame(string path)
        {
            return path == "Assets/MiniGames/Runner/Scenes/Runner.unity"
                || path == "Assets/MiniGames/Snake/Scenes/Snake.unity"
                || path == "Assets/MiniGames/SpaceBoat/Scenes/SpaceBoat.unity"
                || path == "Assets/MiniGames/PopTheLock/Scenes/PopTheLock.unity";
        }

        // Отсчитывает паузу между повторами основной мелодии только вне мини-игры.
        private void Update()
        {
            if (!unlocked || miniGames.Count != 0 || !mainStarted || AudioListener.pause) return;
            if (!waiting && !main.isPlaying)
            {
                waiting = true;
                remainingDelay = settings.repeatDelay;
            }
            else if (waiting)
            {
                remainingDelay -= Time.unscaledDeltaTime;
                if (remainingDelay <= 0f) StartMain();
            }
        }

        // Начинает основную мелодию с начала после победы в Runner или окончания паузы.
        private void StartMain()
        {
            waiting = false;
            mainStarted = true;
            main.Play();
        }

        // Убирает подписки при завершении игры или уничтожении контроллера.
        private void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            if (runner != null) runner.Finished -= RunnerFinished;
            instance = null;
        }
    }
}
