using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Runner
{
    [InitializeOnLoad]
    public static class RunnerVerification
    {
        private const string Root = "Assets/MiniGames/Runner";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int SimulationSteps = 100;
        private const float PlayerHalfHeight = 0.4f;
        private const float LowHeight = 1.3f;
        private const float TallHeight = 2.3f;
        private const string PendingKey = "Runner.Verification.Pending";

        // Восстанавливает обработчик проверки после перезагрузки домена при входе в игровой режим.
        static RunnerVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Собирает мини-игру и выполняет проверки в изолированном тестовом проекте.
        public static void BuildAndVerify()
        {
            Scene host = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(host.path))
                EditorSceneManager.SaveScene(host, Root + "/Editor/VerificationHost.unity");
            if (!System.IO.File.Exists(Root + "/Scenes/Runner.unity")) RunnerSceneBuilder.Build();
            Run();
        }

        // Проверяет готовую сцену, переходы состояний и реальные траектории физики 2D.
        [MenuItem("Tools/Runner/Verify Gameplay")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Запустите проверку вне Play Mode.");
            Scene loaded = SceneManager.GetSceneByPath(Root + "/Scenes/Runner.unity");
            if (loaded.IsValid() && loaded.isLoaded)
                throw new InvalidOperationException("Для проверки откройте другую сохранённую сцену и закройте Runner.unity.");
            RunnerSceneBuilder.Validate();
            VerifyScene();
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        // Выполняет физические проверки после входа Unity в игровой режим и завершает пакетный запуск.
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
            int exitCode = 0;
            try
            {
                float tap = SimulateJump(false);
                float held = SimulateJump(true);
                Require(tap > LowHeight, "Короткий прыжок не преодолевает низкую стену.");
                Require(tap < TallHeight, "Короткий прыжок неожиданно преодолевает высокую стену.");
                Require(held > TallHeight, "Удержание не позволяет преодолеть высокую стену.");
                Debug.Log($"Runner verification PASS: tap={tap:F3}, held={held:F3}; scene, spawn, lose, restart, win, isolation.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else EditorApplication.ExitPlaymode();
        }

        // Проверяет интерфейс, ссылки, спавн за экраном и завершение забега.
        private static void VerifyScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(Root + "/Scenes/Runner.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                RunnerGameManager game = ComponentInScene<RunnerGameManager>(scene);
                PlayerController player = ComponentInScene<PlayerController>(scene);
                ObstacleSpawner spawner = ComponentInScene<ObstacleSpawner>(scene);
                Camera camera = ComponentInScene<Camera>(scene);
                Canvas canvas = ComponentInScene<Canvas>(scene);
                Require(camera.orthographic, "Камера должна быть ортографической.");
                Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Неверный режим Canvas.");
                Require(canvas.GetComponent<CanvasScaler>().uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize,
                    "Неверный режим CanvasScaler.");
                Require(new SerializedObject(game).FindProperty("targetDistance").floatValue == 512f, "Цель должна быть 512 Кб.");
                TMP_Text score = canvas.transform.Find("Score").GetComponent<TMP_Text>();
                Require(score.text == "0 / 512 Кб", "Неверный начальный счёт.");
                Require(score.rectTransform.anchorMin == new Vector2(0.5f, 1f), "Неверный якорь счёта.");
                Call(player, "Awake");
                game.Restart();
                Call(spawner, "Spawn");
                Obstacle spawned = spawner.GetComponentInChildren<Obstacle>();
                Require(spawned != null, "Спавнер не создал стену.");
                Require(spawned.GetComponent<BoxCollider2D>().bounds.min.x > camera.orthographicSize * camera.aspect,
                    "Стена появилась внутри экрана.");
                game.Lose();
                Require(game.State == RunnerGameManager.GameState.Lost, "Поражение не завершает забег.");
                Require(canvas.transform.Find("Result").gameObject.activeSelf, "Сообщение поражения скрыто.");
                UnityEngine.Object.DestroyImmediate(spawned.gameObject);
                game.Restart();
                Require(game.State == RunnerGameManager.GameState.Playing && game.Distance == 0f,
                    "Перезапуск не сбросил забег.");
                typeof(RunnerGameManager).GetMethod("Finish", PrivateInstance).Invoke(game,
                    new object[] { RunnerGameManager.GameState.Won });
                Require(game.State == RunnerGameManager.GameState.Won, "Победа не завершает забег.");
                Require(!player.GetComponent<Rigidbody2D>().simulated, "Игрок движется после завершения.");
                Require(canvas.transform.Find("Result").GetComponent<TMP_Text>().text == "", "После победы показано меню.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        // Измеряет высоту настоящего прыжка в отдельном локальном физическом мире.
        private static float SimulateJump(bool hold)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("Runner Physics Verification " + (hold ? "Held" : "Tap"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            SceneManager.SetActiveScene(scene);
            try
            {
                BoxCollider2D floor = new GameObject("Test Ground").AddComponent<BoxCollider2D>();
                floor.size = new Vector2(100f, 1f);
                floor.transform.position = Vector3.down * 0.5f;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Player.prefab");
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                instance.transform.position = Vector3.up * PlayerHalfHeight;
                Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
                body.position = new Vector2(0f, PlayerHalfHeight);
                PlayerController player = instance.GetComponent<PlayerController>();
                player.Configure(null, floor);
                Call(player, "Awake");
                player.SetRunning(true);
                Physics2D.SyncTransforms();
                typeof(PlayerController).GetField("jumpQueued", PrivateInstance).SetValue(player, true);
                typeof(PlayerController).GetField("jumpHeld", PrivateInstance).SetValue(player, hold);
                float maxHeight = 0f;
                PhysicsScene2D physics = scene.GetPhysicsScene2D();
                for (int step = 0; step < SimulationSteps; step++)
                {
                    Call(player, "FixedUpdate");
                    physics.Simulate(Time.fixedDeltaTime);
                    maxHeight = Mathf.Max(maxHeight, body.position.y - PlayerHalfHeight);
                    Require(Mathf.Abs(body.position.x) < 0.001f, "Игрок перемещается по горизонтали.");
                }
                return maxHeight;
            }
            finally
            {
                SceneManager.UnloadSceneAsync(scene);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        // Находит компонент только среди объектов указанной тестовой сцены.
        private static T ComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            throw new InvalidOperationException("Не найден компонент " + typeof(T).Name);
        }

        // Вызывает этап жизненного цикла компонента для детерминированной проверки в редакторе.
        private static void Call(object target, string method)
        {
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        }

        // Останавливает проверку при нарушении проверяемого условия.
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
