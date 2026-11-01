using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PinchOfTheft.CCTV.Editor
{
    public static class TestSceneSetup
    {
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Cube" || root.GetComponent<Camera>() != null ||
                    root.name == "CCTV" || root.name == "Capsule")
                    Object.DestroyImmediate(root);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            var plane = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "Plane");
            Vector3 center = plane != null ? plane.transform.position : Vector3.zero;
            capsule.transform.position = center + Vector3.up;
            var rootObject = new GameObject("CCTV");
            var groups = new Transform[3];
            Vector3[] positions = { new Vector3(-6, 2, -4), new Vector3(6, 2, -4), new Vector3(0, 2, 6) };
            for (int group = 0; group < 3; group++)
            {
                groups[group] = new GameObject("Group_" + group).transform;
                groups[group].SetParent(rootObject.transform);
                groups[group].position = center + positions[group];
            }
            var views = new CCTVCamera[12];
            for (int i = 0; i < views.Length; i++)
            {
                var go = new GameObject("Cam_" + (i / 4) + (i % 4 + 1));
                go.transform.SetParent(groups[i / 4], false);
                go.transform.localRotation = Quaternion.Euler(0, (i % 4) * 90, 0);
                go.transform.localPosition = go.transform.localRotation * Vector3.forward * 0.5f;
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = 60;
                camera.enabled = i == 0;
                go.AddComponent<AudioListener>().enabled = i == 0;
                views[i] = go.AddComponent<CCTVCamera>();
                var settings = new SerializedObject(views[i]);
                settings.FindProperty("group").intValue = i / 4;
                settings.FindProperty("slot").intValue = i % 4 + 1;
                settings.FindProperty("isVisibleInMenu").boolValue = i != 11;
                settings.FindProperty("isAccessible").boolValue = i != 10;
                settings.FindProperty("maxYawAngle").floatValue = 40f;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            var canvasObject = new GameObject("Camera UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(rootObject.transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var hint = Label("Controls", canvasObject.transform, "TAB - Cameras   Mouse - Look   Q / E - Cameras   N - IR night vision", 26);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 45);
            hint.rectTransform.sizeDelta = new Vector2(900, 60);
            var panel = new GameObject("Camera Menu", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(760, 650);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.97f);
            var title = Label("Title", panel.transform, "SELECT CAMERA", 32);
            title.rectTransform.anchoredPosition = new Vector2(0, 260);
            title.rectTransform.sizeDelta = new Vector2(500, 70);
            var list = new GameObject("Camera Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            list.transform.SetParent(panel.transform, false);
            list.GetComponent<RectTransform>().sizeDelta = new Vector2(460, 450);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var template = new GameObject("Camera Button Template", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            template.transform.SetParent(list.transform, false);
            template.GetComponent<Image>().color = new Color(0.16f, 0.25f, 0.36f);
            template.GetComponent<LayoutElement>().preferredHeight = 75;
            var text = Label("Name", template.transform, "Camera", 28);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            var containers = new Transform[3];
            for (int group = 0; group < 3; group++)
            {
                var row = new GameObject("Group " + group + "x", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(list.transform, false);
                row.GetComponent<LayoutElement>().preferredHeight = 100;
                var grid = row.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(100, 75);
                grid.spacing = new Vector2(15, 0);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                containers[group] = row.transform;
            }
            template.transform.SetParent(panel.transform, false);
            var menu = canvasObject.AddComponent<CameraMenuUI>();
            var crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(Image), typeof(Outline));
            crosshair.transform.SetParent(canvasObject.transform, false);
            var aim = crosshair.GetComponent<RectTransform>();
            aim.anchorMin = aim.anchorMax = aim.pivot = new Vector2(0.5f, 0.5f);
            aim.anchoredPosition = Vector2.zero;
            aim.sizeDelta = new Vector2(5, 5);
            crosshair.GetComponent<Image>().raycastTarget = false;
            crosshair.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            crosshair.GetComponent<Outline>().effectDistance = Vector2.one;
            var cameraLabel = Label("CameraLabel", canvasObject.transform, "CAM 01", 36);
            cameraLabel.rectTransform.anchorMin = cameraLabel.rectTransform.anchorMax = new Vector2(0, 1);
            cameraLabel.rectTransform.pivot = new Vector2(0, 1);
            cameraLabel.rectTransform.anchoredPosition = new Vector2(24, -24);
            cameraLabel.rectTransform.sizeDelta = new Vector2(320, 60);
            cameraLabel.alignment = TextAnchor.MiddleLeft;
            var shadow = cameraLabel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.9f);
            shadow.effectDistance = new Vector2(2, -2);
            menu.Configure(panel, containers, template.GetComponent<Button>(), crosshair, cameraLabel);
            template.SetActive(false);
            panel.SetActive(false);
            rootObject.AddComponent<CameraManager>().Configure(views, menu);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(rootObject.transform);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CCTV_SETUP_COMPLETE");
        }

        private static Text Label(string name, Transform parent, string value, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}




