using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PopTheLock
{
    [InitializeOnLoad]
    public static class LockVerification
    {
        private const string Root = "Assets/MiniGames/PopTheLock";
        private const string Pending = "PopTheLock.Verify";
        private static int ticks;

        // Подключает проверку сцены после перезагрузки домена при входе в игровой режим.
        static LockVerification()
        {
            EditorApplication.update += VerifyScene;
        }

        // Проверяет попадания, промахи, произвольный порядок и завершение раунда.
        [MenuItem("Tools/PopTheLock/Verify Rules")]
        public static void VerifyRules()
        {
            var round = new LockRound(8, 90f, 10f, 180f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            int[] order = { 6, 2, 7, 0, 4, 1, 5, 3 };
            foreach (int index in order)
            {
                float distance = round.AngularSpeed > 0f
                    ? (round.LockAngle(index) - round.KeyAngle + 360f) % 360f
                    : (round.KeyAngle - round.LockAngle(index) + 360f) % 360f;
                round.Advance(distance / 90f);
                float previousSpeed = round.AngularSpeed;
                Require(round.Press(), "Не принят замок в произвольном порядке.");
                Require(round.AngularSpeed == -previousSpeed, "Попадание не меняет направление.");
                Require(!round.Press(), "Повторное нажатие во время притяжения принято.");
                Require(round.AngularSpeed == -previousSpeed, "Отклонённое нажатие меняет направление.");
                round.Advance(0.1f);
                Require(round.IsOpen(index) && round.InwardAmount == 1f, "Замок не открыт при достижении ключом.");
                round.Advance(0.21f);
            }
            Require(round.Won && round.OpenedCount == 8 && round.InwardAmount == 0f, "Победа наступила неверно.");
            Require(round.Lives == 3, "Правильные попадания отнимают жизни.");
            float finalAngle = round.KeyAngle;
            round.Advance(100f);
            Require(!round.Press() && round.KeyAngle == finalAngle, "Игра движется после победы.");
            var miss = new LockRound(8, 100f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            Require(!miss.Press(), "Промах засчитан как попадание.");
            miss.Advance(0.2f);
            Require(Math.Abs(miss.InwardAmount - 0.25f) < 0.001f && Math.Abs(miss.KeyAngle - 2.5f) < 0.001f,
                "При промахе ключ не приближается или не вращается в обратную сторону.");
            miss.Advance(0.2f);
            Require(miss.Phase == KeyPhase.Orbit && miss.OpenedCount == 0 && miss.InwardAmount == 0f,
                "Промах остановил игру или открыл замок.");
            var wrap = new LockRound(8, 0f, 4f, 359f, 0f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            Require(wrap.Press(), "Не работает попадание через границу углов 0/360.");
            wrap.Advance(1f);
            Require(wrap.OpenedCount == 1 && !wrap.Press(), "Открытый замок можно открыть повторно.");
            var lives = new LockRound(8, 0f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
            for (int expected = 2; expected >= 0; expected--)
            {
                Require(!lives.Press() && lives.Lives == expected, "Промах не отнимает ровно одну жизнь.");
                lives.Press();
                Require(lives.Lives == expected, "Нажатие во время анимации отнимает жизнь.");
                lives.Advance(0.4f);
                Require(lives.Lost == (expected == 0), "Поражение наступает не на последней жизни.");
            }
            float lostAngle = lives.KeyAngle;
            lives.Press();
            lives.Advance(5f);
            Require(lives.Lives == 0 && lives.KeyAngle == lostAngle, "Игра продолжается после поражения.");
            var customLives = new LockRound(8, 0f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f, 5);
            Require(customLives.Lives == 5, "Настраиваемое число жизней не применяется.");
            Debug.Log("PopTheLock lives PASS: default 3, custom 5, miss decrement, ignored input, loss, freeze, hits preserve lives.");
            Debug.Log("PopTheLock rules PASS: reverse on hit and miss, ignored input guard, arbitrary order, wraparound, already-open lock, victory freeze.");
        }

        // Проверяет отсутствие ссылок на игровые ресурсы других директорий.
        [MenuItem("Tools/PopTheLock/Validate Isolation")]
        public static void ValidateIsolation()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || path.EndsWith(".cs") || path.EndsWith(".asmdef")) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    if (dependency.StartsWith("Assets/") && !dependency.StartsWith(Root + "/"))
                        throw new InvalidOperationException("Внешний ресурс: " + dependency);
            }
            Debug.Log("PopTheLock isolation PASS");
        }

        // Запускает полный набор проверок исключительно во временном проекте внутри PopTheLock.
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Contains(".Validation~"))
                throw new InvalidOperationException("Нужен отдельный временный проект.");
            VerifyRules();
            ValidateIsolation();
            EditorSceneManager.OpenScene(Root + "/Scenes/PopTheLock.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        // Проверяет камеру, число замков, окружности и сохранение пропорций после запуска сцены.
        private static void VerifyScene()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false)) return;
            if (++ticks < 5) return;
            SessionState.SetBool(Pending, false);
            int exitCode = 0;
            try
            {
                GameObject root = GameObject.Find("PopTheLock");
                Require(root != null && root.GetComponentInChildren<Camera>().orthographic, "Не создана камера.");
                Require(root.GetComponent<PopTheLockGame>().Round.Count == 8, "Неверное число замков.");
                SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>();
                Require(sprites.Length == 11, "Не созданы сервер, восемь замков, ключ и фон.");
                Require(root.GetComponentsInChildren<Text>().Length == 2, "Не создан интерфейс.");
                foreach (SpriteRenderer sprite in sprites)
                    Require(sprite.sprite != null && sprite.sharedMaterial != null
                        && Mathf.Approximately(sprite.transform.localScale.x, sprite.transform.localScale.y),
                        "Не назначен ресурс или нарушены пропорции.");
                Require(root.transform.Find("Orbiting Key").localPosition.magnitude
                    > root.transform.Find("Lock 1").localPosition.magnitude, "Ключ расположен не на внешней окружности.");
                LivesView lifeView = root.GetComponentInChildren<LivesView>();
                Require(lifeView.GetComponentsInChildren<Image>().Length == 3, "Не показаны три ключа жизни.");
                Require(lifeView.GetComponent<RectTransform>().anchorMax == Vector2.one, "Жизни не закреплены справа сверху.");
                foreach (Image icon in lifeView.GetComponentsInChildren<Image>())
                    Require(icon.sprite != null && icon.preserveAspect, "Иконка ключа потеряла спрайт или пропорции.");
                var loss = new LockRound(8, 0f, 10f, 22.5f, 90f, 0.1f, 0.1f, 0.1f, 0.4f, 0.25f);
                for (int remaining = 2; remaining >= 0; remaining--)
                {
                    loss.Press();
                    loss.Advance(0.4f);
                    root.GetComponent<LockView>().Render(loss);
                    Require(lifeView.GetComponentsInChildren<Image>().Length == remaining, "Иконки не уменьшаются после промаха.");
                }
                root.GetComponent<PopTheLockGame>().Restart();
                Require(lifeView.GetComponentsInChildren<Image>().Length == 3
                    && root.GetComponent<PopTheLockGame>().Round.OpenedCount == 0
                    && root.GetComponentsInChildren<SpriteRenderer>().Length == 11,
                    "Перезапуск не восстанавливает жизни или дублирует объекты.");
                Debug.Log("PopTheLock lives UI PASS: top-right keys, 3-2-1-0, preserved aspect, restart without duplicates.");
                Debug.Log("PopTheLock scene PASS: camera, eight locks, server, outer key orbit, UI, uniform scaling.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        // Останавливает проверку при нарушении ожидаемого условия.
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
