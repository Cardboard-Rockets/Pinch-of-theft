using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Snake
{
    [InitializeOnLoad]
    public static class SnakeVerification
    {
        private const string Root = "Assets/MiniGames/Snake";
        private const string PendingKey = "Snake.Validation.Pending";
        private static int ticks;

        // Подключает проверку после входа в игровой режим тестового проекта.
        static SnakeVerification()
        {
            EditorApplication.update += CheckPlayMode;
        }

        // Проверяет прямые ссылки ресурсов Snake без изменения настроек проекта.
        [MenuItem("Tools/Snake/Validate Isolation")]
        public static void ValidateIsolation()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || path.EndsWith(".cs") || path.EndsWith(".asmdef")) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (dependency.StartsWith("Assets/") && !dependency.StartsWith(Root + "/"))
                        throw new InvalidOperationException("Внешний ресурс: " + dependency);
                }
            }
            Debug.Log("Snake isolation PASS");
        }

        // Запускает проверку загрузки сцены исключительно в отдельном временном проекте.
        public static void ValidateBatch()
        {
            if (!Application.isBatchMode || !Application.dataPath.Contains(".Validation~"))
                throw new InvalidOperationException("Пакетная проверка разрешена только во временном проекте.");
            ValidateIsolation();
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Materials/SnakeSprite.shader");
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ошибка шейдера Snake.");
            EditorSceneManager.OpenScene(Root + "/Scenes/Snake.unity");
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        // Проверяет созданные камерой и представлением объекты после запуска сцены.
        private static void CheckPlayMode()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(PendingKey, false)) return;
            ticks++;
            if (ticks < 10) return;
            SessionState.SetBool(PendingKey, false);
            int code = 0;
            try
            {
                GameObject root = GameObject.Find("Snake");
                if (root == null || root.GetComponentInChildren<Camera>() == null)
                    throw new InvalidOperationException("Сцена или камера не созданы.");
                if (root.GetComponentsInChildren<Text>().Length != 2)
                    throw new InvalidOperationException("Не создан интерфейс.");
                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length < 10) throw new InvalidOperationException("Не созданы сегменты, еда или ячейки.");
                foreach (SpriteRenderer renderer in renderers)
                {
                    if (renderer.sprite == null || renderer.sharedMaterial == null)
                        throw new InvalidOperationException("Не назначен спрайт или материал.");
                    Vector3 scale = renderer.transform.localScale;
                    if (!Mathf.Approximately(scale.x, scale.y))
                        throw new InvalidOperationException("Нарушены пропорции: " + renderer.name);
                }
                Debug.Log("Snake scene PASS: Play Mode, camera, UI, sprites, local materials, uniform scaling.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                code = 1;
            }
            EditorApplication.Exit(code);
        }
    }
}
