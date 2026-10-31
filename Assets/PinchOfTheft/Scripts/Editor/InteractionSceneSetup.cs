using System.Linq;
using PinchOfTheft.CCTV;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction.Editor
{
    public static class InteractionSceneSetup
    {
        public static void AdjustPositions()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var manager = Object.FindAnyObjectByType<CameraManager>();
            for (int group = 0; group < 2; group++)
            {
                var sphere = GameObject.Find(group == 0 ? "Sphere" : "Sphere (1)");
                var camera = manager.Cameras.First(c => c.Group == group && c.Slot == 1);
                sphere.transform.position = camera.transform.position + camera.transform.forward;
                var collider = sphere.GetComponent<Collider>();
                collider.enabled = true; collider.isTrigger = false;
            }
            EditorSceneManager.SaveScene(scene);
        }
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var manager = Object.FindAnyObjectByType<CameraManager>();
            var menu = Object.FindAnyObjectByType<CameraMenuUI>();
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            int layer = LayerMask.NameToLayer("Interactable");
            var layers = tags.FindProperty("layers");
            if (layer < 0)
                for (int i = 8; i < 32; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    { layers.GetArrayElementAtIndex(i).stringValue = "Interactable"; layer = i; break; }
            if (layer < 0) throw new System.Exception("No free layer");
            tags.ApplyModifiedPropertiesWithoutUndo();
            foreach (var camera in manager.Cameras)
                if (camera != null && camera.Group > 0)
                { camera.SetAccessible(false); camera.SetVisibleInMenu(false); }
            var interactor = new GameObject("Interactor").AddComponent<SphereInteractor>();
            Set(interactor, "cameraManager", manager); Set(interactor, "menu", menu); Set(manager, "interactor", interactor);
            var settings = new SerializedObject(interactor);
            settings.FindProperty("interactMask").intValue = 1 << layer;
            settings.ApplyModifiedPropertiesWithoutUndo();
            for (int group = 0; group < 2; group++)
            {
                var sphere = GameObject.Find(group == 0 ? "Sphere" : "Sphere (1)");
                if (sphere == null) sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if (sphere.GetComponent<Collider>() == null) sphere.AddComponent<SphereCollider>();
                sphere.layer = layer;
                var camera = manager.Cameras.First(c => c != null && c.Group == group && c.Slot == 1);
                sphere.transform.position = camera.transform.position + camera.transform.forward * 3f;
                sphere.transform.localScale = Vector3.one * 0.7f;
                var interaction = sphere.AddComponent<InteractableSphere>();
                Set(interaction, "cameraManager", manager);
                var serialized = new SerializedObject(interaction);
                serialized.FindProperty("unlockGroup").intValue = group + 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var canvasObject = new GameObject("KeyPrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                canvasObject.transform.SetParent(sphere.transform, false);
                canvasObject.transform.localPosition = new Vector3(0.7f, 0.7f, 0);
                canvasObject.transform.localScale = Vector3.one * (0.004f / 0.7f);
                canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var alpha = canvasObject.GetComponent<CanvasGroup>(); alpha.alpha = 0.5f; alpha.blocksRaycasts = false;
                var background = Image("Background", canvasObject.transform); background.color = new Color(0.1f, 0.1f, 0.1f);
                var fill = Image("Fill", canvasObject.transform);
                fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                fill.type = UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                fill.fillOrigin = 0; fill.fillAmount = 0; fill.color = new Color(0.6f, 0.8f, 1);
                var textObject = new GameObject("E", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(canvasObject.transform, false);
                textObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
                var text = textObject.GetComponent<Text>(); text.text = "E"; text.fontSize = 64;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                textObject.AddComponent<Shadow>();
                var prompt = canvasObject.AddComponent<KeyPrompt>();
                Set(prompt, "cameraManager", manager); Set(prompt, "canvasGroup", alpha); Set(prompt, "fill", fill);
                Set(interaction, "prompt", prompt);
            }
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("INTERACTION_SETUP_COMPLETE");
        }
        private static Image Image(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}


