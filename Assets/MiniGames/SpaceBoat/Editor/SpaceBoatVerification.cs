using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceBoat
{
    [InitializeOnLoad]
    public static class SpaceBoatVerification
    {
        private const string Root = "Assets/MiniGames/SpaceBoat";
        private const string Pending = "SpaceBoat.Verify";
        private static int ticks;

        // Восстанавливает обработчик проверки после входа редактора в игровой режим.
        static SpaceBoatVerification()
        {
            EditorApplication.update += VerifyPlaying;
        }

        // Проверяет локальность прямых ссылок на ресурсы мини-игры.
        [MenuItem("Tools/SpaceBoat/Validate Isolation")]
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
            Debug.Log("SpaceBoat isolation PASS");
        }

        // Запускает проверку сцены только в отдельном временном проекте внутри SpaceBoat.
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Contains(".Validation~"))
                throw new InvalidOperationException("Нужен отдельный временный проект.");
            ValidateIsolation();
            EditorSceneManager.OpenScene(Root + "/Scenes/SpaceBoat.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        // Проверяет строй, пропорции, попадания, победу, поражение и перезапуск после запуска сцены.
        private static void VerifyPlaying()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false)) return;
            if (++ticks < 5) return;
            SessionState.SetBool(Pending, false);
            int exitCode = 0;
            try
            {
                GameObject root = GameObject.Find("SpaceBoat");
                SpaceBoatGame game = root.GetComponent<SpaceBoatGame>();
                Invader[] enemies = root.GetComponentsInChildren<Invader>();
                Require(enemies.Length == 21, "Неверное число противников.");
                Camera camera = root.GetComponentInChildren<Camera>();
                foreach (Invader enemy in enemies)
                    Require(enemy.Bounds.min.y > camera.orthographicSize, "Строй появляется внутри экрана.");
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>())
                    Require(renderer.sprite != null && Mathf.Approximately(renderer.transform.localScale.x,
                        renderer.transform.localScale.y), "Нарушены пропорции.");
                game.Shoot(game.Player.transform.position);
                Require(root.GetComponentsInChildren<LaserProjectile>().Length == 1, "Не создаётся лазер.");
                Require(!game.HitAlongPath(new Vector2(-100f, -10f), new Vector2(-100f, 100f), Vector2.one), "Ложное попадание.");
                foreach (Invader enemy in enemies)
                {
                    float x = enemy.Bounds.center.x;
                    Require(game.HitAlongPath(new Vector2(x, -8f), new Vector2(x, 100f), new Vector2(0.1f, 0.4f)),
                        "Пропущено попадание на длинном пути.");
                }
                Require(game.State == GameState.Won, "Победа не наступила.");
                game.Restart();
                Require(game.State == GameState.Playing && root.GetComponentsInChildren<Invader>().Length == 21,
                    "Не восстановлен строй.");
                Require(root.GetComponentsInChildren<LaserProjectile>().Length == 0, "Старый лазер остался после перезапуска.");
                game.Lose();
                Require(game.State == GameState.Lost, "Поражение не наступило.");
                Debug.Log("SpaceBoat gameplay PASS: scene, 21 offscreen enemies, uniform sprites, laser, swept hits, win, restart, loss.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        // Прерывает проверку при нарушении ожидаемого игрового поведения.
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
