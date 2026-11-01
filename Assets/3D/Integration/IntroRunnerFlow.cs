using PinchOfTheft.Core;
using Runner;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniGames.Integration
{
    public sealed class IntroRunnerFlow : MonoBehaviour
    {
        [SerializeField] private VanIntroSequence intro;
        [SerializeField] private string runnerScene = "Assets/MiniGames/Runner/Scenes/Runner.unity";
        [SerializeField] private string houseScene = "Assets/Scenes/House.unity";
        private RunnerGameManager runner;
        private bool loading;

        // Сохраняет связующий объект между сценами и подписывается на завершение вступления.
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            intro.Completed += IntroCompleted;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        // После окончания вступления запускает Runner с полной заменой сцены.
        private void IntroCompleted()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(runnerScene) || !Application.CanStreamedLevelBeLoaded(houseScene))
            {
                Debug.LogError("Intro flow: Runner or House is missing from the scene list.", this);
                return;
            }
            loading = true;
            intro.Completed -= IntroCompleted;
            SceneManager.LoadSceneAsync(runnerScene, LoadSceneMode.Single);
        }

        // Подключает результат Runner после загрузки и удаляет связующий объект при выходе из цепочки.
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != runnerScene)
            {
                if (loading) Destroy(gameObject);
                return;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                runner = root.GetComponentInChildren<RunnerGameManager>(true);
                if (runner != null) break;
            }
            if (runner == null)
            {
                Debug.LogError("Intro flow: RunnerGameManager is missing.", this);
                Destroy(gameObject);
                return;
            }
            runner.Finished += RunnerFinished;
        }

        // Переходит в дом только после победы и сохраняет повторную попытку после поражения.
        private void RunnerFinished(RunnerGameManager.GameState state)
        {
            if (state != RunnerGameManager.GameState.Won) return;
            runner.Finished -= RunnerFinished;
            SceneManager.LoadSceneAsync(houseScene, LoadSceneMode.Single);
        }

        // Освобождает подписки при завершении переходов или остановке игры.
        private void OnDestroy()
        {
            if (intro != null) intro.Completed -= IntroCompleted;
            if (runner != null) runner.Finished -= RunnerFinished;
            SceneManager.sceneLoaded -= SceneLoaded;
        }
    }
}
