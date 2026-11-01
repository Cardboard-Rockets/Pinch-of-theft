using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Runner
{
    public static class RunnerSceneBuilder
    {
        private const string Root = "Assets/MiniGames/Runner";
        private const string ScenePath = Root + "/Scenes/Runner.unity";
        private const string FontPath = Root + "/UI/Fonts/RunnerFont.asset";
        private const float CameraSize = 5f;
        private const float FloorY = -4.1f;
        private const float FloorThickness = 1f;
        private const float PlayerSize = 0.8f;
        private const float LowHeight = 1.3f;
        private const float TallHeight = 2.3f;
        private const float WallWidth = 0.8f;
        private const int AtlasSize = 1024;
        private const int FontPointSize = 64;
        private const int FontPadding = 7;
        private const float PixelsPerUnit = 100f;

        // Создаёт сцену и префабы только внутри папки Runner через меню редактора.
        [MenuItem("Tools/Runner/Build Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Сцена уже существует. Откройте её; сборщик не перезаписывает готовую сцену.");
            foreach (string directory in new[] { "Scenes", "Prefabs", "Materials", "UI/Resources" })
                Directory.CreateDirectory(Root + "/" + directory);
            AssetDatabase.Refresh();
            Sprite playerSprite = ImportSprite("PersSPervogoYrovnya (1).png");
            Sprite backgroundSprite = ImportSprite("FonIgriOdin.png");
            Sprite lowSprite = ImportSprite("firewall1.gif");
            Sprite tallSprite = ImportSprite("firewallTall (1).gif");
            Material spriteMaterial = CreateSpriteMaterial();
            CreateSettings(null);
            TMP_Settings.LoadDefaultSettings();
            TMP_FontAsset font = CreateFont();
            CreateSettings(font);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                CreateScene(playerSprite, backgroundSprite, lowSprite, tallSprite, spriteMaterial, font);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Не удалось сохранить сцену Runner.");
                AssetDatabase.SaveAssets();
                Validate();
                Debug.Log("Runner: сцена создана. Откройте " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        // Импортирует выбранное исходное изображение как спрайт без создания новой текстуры.
        private static Sprite ImportSprite(string filename)
        {
            string path = Root + "/Art/" + filename;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Не найдена текстура: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Unity не импортировала спрайт: " + path);
            return sprite;
        }

        // Создаёт локальный материал спрайтов с независимым от освещения шейдером.
        private static Material CreateSpriteMaterial()
        {
            string path = Root + "/Materials/RunnerSprite.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Materials/RunnerSprite.shader");
            material = new Material(shader) { name = "RunnerSprite" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Создаёт локальный статический атлас TMP с латиницей и кириллицей из выбранного шрифта.
        private static TMP_FontAsset CreateFont()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null) return existing;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/Arial.ttf");
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, FontPointSize, FontPadding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            font.name = "RunnerFont";
            string characters = string.Concat(Enumerable.Range(32, 95).Select(ToCharacter))
                + string.Concat(Enumerable.Range(0x410, 64).Select(ToCharacter)) + "Ёё—";
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("В шрифте отсутствуют символы: " + missing);
            font.material.shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/UI/Shaders/TMP_SDF-Mobile.shader");
            font.material.name = "RunnerFont Material";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                atlas.name = "RunnerFont Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        // Преобразует код символа в строку для подготовки набора глифов.
        private static string ToCharacter(int code)
        {
            return char.ConvertFromUtf32(code);
        }

        // Создаёт локальные настройки TMP без внешних шрифтов и запасных ресурсов.
        private static void CreateSettings(TMP_FontAsset font)
        {
            string path = Root + "/UI/Resources/TMP Settings.asset";
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(path);
            bool isNew = settings == null;
            if (isNew) settings = ScriptableObject.CreateInstance<TMP_Settings>();
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_fallbackFontAssets").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (isNew) AssetDatabase.CreateAsset(settings, path);
            else EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Собирает ортографическую сцену с игроком, фоном, полом, интерфейсом и спавнером.
        private static void CreateScene(Sprite playerSprite, Sprite backgroundSprite, Sprite lowSprite,
            Sprite tallSprite, Material material, TMP_FontAsset font)
        {
            Camera camera = new GameObject("Runner Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.07f, 0.09f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            GameObject background = CreateVisual("Static Background", backgroundSprite, material, -10,
                new Vector2(CameraSize * 2f * 16f / 9f, CameraSize * 2f));
            background.transform.position = new Vector3(0f, 0f, 1f);
            background.AddComponent<StaticBackgroundFit>().Configure(camera);
            BoxCollider2D floor = new GameObject("Invisible Ground").AddComponent<BoxCollider2D>();
            floor.size = new Vector2(200f, FloorThickness);
            floor.transform.position = new Vector3(0f, FloorY - FloorThickness * 0.5f, 0f);
            RunnerGameManager game = new GameObject("Runner Game Manager").AddComponent<RunnerGameManager>();
            GameObject playerObject = CreateVisual("Player", playerSprite, material, 1, Vector2.one * PlayerSize);
            playerObject.transform.position = new Vector3(0f, FloorY + PlayerSize * 0.5f, 0f);
            Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            playerBody.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            BoxCollider2D playerCollider = playerObject.AddComponent<BoxCollider2D>();
            playerCollider.size = Vector2.one * PlayerSize;
            PlayerController player = playerObject.AddComponent<PlayerController>();
            GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerObject, Root + "/Prefabs/Player.prefab");
            if (playerPrefab == null) throw new IOException("Не удалось создать префаб игрока.");
            player.Configure(game, floor);
            Obstacle low = CreateWall("LowWall", lowSprite, material, LowHeight);
            Obstacle tall = CreateWall("TallWall", tallSprite, material, TallHeight);
            ObstacleSpawner spawner = new GameObject("Obstacle Spawner").AddComponent<ObstacleSpawner>();
            spawner.Configure(game, camera, new[] { low, tall }, FloorY);
            ScoreView view = CreateUI(font, game);
            game.Configure(player, spawner, view);
        }

        // Создаёт объект со спрайтом нужного размера без масштабирования физических компонентов.
        private static GameObject CreateVisual(string name, Sprite sprite, Material material, int order, Vector2 size)
        {
            GameObject root = new GameObject(name);
            GameObject visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            visual.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
            return root;
        }

        // Сохраняет отдельный префаб стены с заданной высотой спрайта и коллайдера.
        private static Obstacle CreateWall(string name, Sprite sprite, Material material, float height)
        {
            string folder = Root + "/Art/WallFrames/" + name;
            Sprite[] frames = Enumerable.Range(0, 4)
                .Select(index => AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/Frame" + index + ".png")).ToArray();
            if (frames.Any(frame => frame == null))
                throw new InvalidOperationException("Не найдены кадры стены: " + folder);
            sprite = frames[0];
            float width = height * sprite.bounds.size.x / sprite.bounds.size.y;
            GameObject wall = CreateVisual(name, sprite, material, 2, new Vector2(width, height));
            Rigidbody2D body = wall.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, height);
            wall.AddComponent<Obstacle>();
            wall.AddComponent<WallAnimation>().Configure(wall.GetComponentInChildren<SpriteRenderer>(), frames,
                ReadGifDurations(name));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wall, Root + "/Prefabs/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(wall);
            return prefab.GetComponent<Obstacle>();
        }

        // Читает длительности кадров из сохранённого описания исходной GIF-анимации.
        private static float[] ReadGifDurations(string name)
        {
            string path = Root + "/Art/WallFrames/" + name + "/Durations.txt";
            return File.ReadAllLines(path).Select(value => float.Parse(value,
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }

        // Собирает интерфейс uGUI с локальным шрифтом TMP и верхним центральным счётом.
        private static ScoreView CreateUI(TMP_FontAsset font, RunnerGameManager game)
        {
            GameObject root = new GameObject("Runner UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            TMP_Text score = CreateText(root.transform, "Score", font, new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(600f, 80f), 48f);
            score.text = "0 / 512 Кб";
            TMP_Text result = CreateText(root.transform, "Result", font, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1400f, 240f), 48f);
            result.gameObject.SetActive(false);
            ScoreView view = root.AddComponent<ScoreView>();
            view.Configure(score, result, game);
            return view;
        }

        // Создаёт текстовое поле с явным шрифтом, материалом и якорями.
        private static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, Vector2 anchor,
            Vector2 position, Vector2 size, float fontSize)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            TextMeshProUGUI text = item.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(225, 248, 210, 255);
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return text;
        }

        // Проверяет зависимости сцены и префабов на отсутствие ссылок на чужие ресурсы.
        [MenuItem("Tools/Runner/Validate Isolation")]
        public static void Validate()
        {
            string[] assets = AssetDatabase.FindAssets("", new[] { Root });
            foreach (string guid in assets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || path.EndsWith(".cs", StringComparison.Ordinal)
                    || path.EndsWith(".asmdef", StringComparison.Ordinal)) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (dependency.StartsWith("Assets/", StringComparison.Ordinal)
                        && !dependency.StartsWith(Root + "/", StringComparison.Ordinal)
                        && dependency != Root)
                        throw new InvalidOperationException("Внешняя ссылка: " + path + " -> " + dependency);
                    if (dependency.StartsWith("Packages/", StringComparison.Ordinal)
                        && !dependency.EndsWith(".cs", StringComparison.Ordinal)
                        && !dependency.EndsWith(".asmdef", StringComparison.Ordinal)
                        && !dependency.EndsWith(".dll", StringComparison.Ordinal))
                        throw new InvalidOperationException("Внешний ресурс пакета: " + dependency);
                }
            }
            Debug.Log("Runner: проверка изоляции пройдена.");
        }
    }
}
