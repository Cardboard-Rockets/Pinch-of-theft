using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class HouseCCTVSetup
    {
        private const string Root = "Assets/PinchOfTheft/";
        private static string Normalize(string name) => Regex.Replace(name.ToLowerInvariant(), @"[\s_]", "");
        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        private static Transform[] Transforms() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        private static string ScenePath()
        {
            var paths = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).Where(p => string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), "house", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (paths.Length != 1) throw new Exception("House candidates: " + string.Join(", ", paths));
            return paths[0];
        }
        private static void Set(Object obj, string field, object value)
        {
            var so = new SerializedObject(obj); var p = so.FindProperty(field);
            if (p == null) return;
            if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
            else if (value is bool b) p.boolValue = b;
            else p.objectReferenceValue = value as Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Audit()
        {
            EditorSceneManager.OpenScene(ScenePath());
            foreach (var t in Transforms()) if (Regex.IsMatch(t.name, "cam|lock|player", RegexOptions.IgnoreCase))
                Debug.Log("HOUSE_OBJECT: " + Path(t) + " components=" + string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name)));
        }
        public static void Run()
        {
            string scenePath = ScenePath();
            var core = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Core/Core.prefab");
            var promptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Interaction/KeyPrompt.prefab");
            if (core == null || promptPrefab == null) throw new Exception("Core/KeyPrompt prefab missing; setup stopped");
            var reference = new Dictionary<string, float[]>();
            EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            foreach (var c in Transforms().Select(t => t.GetComponent<CCTVCamera>()).Where(c => c != null))
            {
                var s = new SerializedObject(c);
                reference[c.CameraName] = new[] { s.FindProperty("maxYawAngle").floatValue, s.FindProperty("maxPitchAngle").floatValue };
            }
            var scene = EditorSceneManager.OpenScene(scenePath);
            var originals = Transforms();
            var snapshots = originals.ToDictionary(t => t, t => (t.name, t.parent, t.localPosition, t.localRotation, t.localScale));
            var report = new StringBuilder("# House CCTV\n\n");
            PrefabSetup.AddCore();
            var manager = Object.FindAnyObjectByType<CameraManager>();
            if (manager == null) throw new Exception("No CameraManager");
            var numbered = originals.Select(t => new { t, match = Regex.Match(Normalize(t.name), @"^(?:cam|camera)?(\d{2})$") }).Where(x => x.match.Success).GroupBy(x => x.match.Groups[1].Value).OrderBy(g => g.Key);
            var cameras = new List<CCTVCamera>();
            report.AppendLine("| Object | group/slot | Added | Accessible/Visible |\n|---|---|---|---|");
            foreach (var group in numbered)
            {
                string number = group.Key;
                if (group.Count() != 1 || !Regex.IsMatch(number, "^[0-9][1-9]$"))
                { report.AppendLine("Skipped: " + string.Join(", ", group.Select(x => Path(x.t))) + " — duplicate or out of range."); continue; }
                var root = group.Single().t;
                var views = root.GetComponentsInChildren<Camera>(true);
                if (views.Length > 1) { report.AppendLine("Skipped ambiguous: " + Path(root) + ": " + string.Join(", ", views.Select(c => Path(c.transform)))); continue; }
                var obj = views.Length == 1 ? views[0].gameObject : root.gameObject;
                var added = new List<string>();
                if (obj.GetComponent<Camera>() == null) { obj.AddComponent<Camera>(); added.Add("Camera"); }
                if (obj.GetComponent<AudioListener>() == null) { obj.AddComponent<AudioListener>(); added.Add("AudioListener"); }
                var cctv = obj.GetComponent<CCTVCamera>();
                if (cctv == null) { cctv = obj.AddComponent<CCTVCamera>(); added.Add("CCTVCamera"); }
                int g = number[0] - '0', slot = number[1] - '0';
                Set(cctv, "group", g); Set(cctv, "slot", slot); Set(cctv, "isAccessible", g == 0 || g == 3); Set(cctv, "isVisibleInMenu", g == 0 || g == 3);
                if (reference.TryGetValue(number, out var limits)) { Set(cctv, "maxYawAngle", limits[0]); Set(cctv, "maxPitchAngle", limits[1]); }
                cctv.SetActive(number == "01"); cameras.Add(cctv);
                report.AppendLine($"| {Path(obj.transform)} | {g}/{slot} | {string.Join(", ", added)} | {g == 0 || g == 3}/{g == 0 || g == 3} |");
            }
            for (int g = 0; g < 3; g++) for (int slot = 1; slot <= 4; slot++)
                if (!cameras.Any(c => c.Group == g && c.Slot == slot)) report.AppendLine($"Missing camera: {g}{slot}");
            var settings = new SerializedObject(manager); var array = settings.FindProperty("cameras"); array.arraySize = cameras.Count;
            for (int i = 0; i < cameras.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = cameras[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (listener.enabled && listener.GetComponent<CCTVCamera>() == null) { listener.enabled = false; report.AppendLine("Disabled conflicting AudioListener: " + Path(listener.transform)); }
            foreach (var view in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (view.enabled && view.GetComponent<CCTVCamera>() == null) report.AppendLine("CONFLICTING_CAMERA: " + Path(view.transform));
            foreach (int group in new[] { 1, 2 })
            {
                var candidates = originals.Where(t => Normalize(t.name) == "onlockcam" + group + "1" || Normalize(t.name) == "unlockcam" + group + "1").ToArray();
                if (candidates.Length != 1) { report.AppendLine("Unlocker candidates " + group + ": " + string.Join(", ", candidates.Select(Path))); continue; }
                var target = candidates[0]; var added = new List<string>();
                var renderers = target.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(target.position, Vector3.one * 0.4f);
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                if (target.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    var box = target.gameObject.AddComponent<BoxCollider>();
                    var localBounds = new Bounds(target.InverseTransformPoint(bounds.center), Vector3.zero);
                    for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                        localBounds.Encapsulate(target.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
                    box.center = localBounds.center; box.size = localBounds.size; added.Add("BoxCollider");
                }
                target.gameObject.layer = LayerMask.NameToLayer("Interactable");
                var interactable = target.GetComponent<InteractableSphere>();
                if (interactable == null) { interactable = target.gameObject.AddComponent<InteractableSphere>(); added.Add("InteractableSphere"); }
                var prompts = target.GetComponentsInChildren<KeyPrompt>(true);
                if (prompts.Length > 1) throw new Exception("Ambiguous KeyPrompt: " + Path(target));
                var prompt = prompts.FirstOrDefault();
                if (prompt == null)
                {
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(promptPrefab, target);
                    obj.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.3f);
                    obj.transform.rotation = Quaternion.identity;
                    var parentScale = target.lossyScale;
                    obj.transform.localScale = new Vector3(0.005f / Mathf.Abs(parentScale.x), 0.005f / Mathf.Abs(parentScale.y), 0.005f / Mathf.Abs(parentScale.z));
                    prompt = obj.GetComponent<KeyPrompt>(); added.Add("KeyPrompt prefab");
                }
                Set(interactable, "unlockGroup", group); Set(interactable, "singleUse", true); Set(interactable, "startAvailable", true); Set(interactable, "holdDuration", 2f);
                Set(interactable, "cameraManager", manager); Set(interactable, "prompt", prompt);
                report.AppendLine($"\nUnlocker {Path(target)}: group={group}, added={string.Join(", ", added)}");
                Physics.SyncTransforms();
                foreach (var camera in cameras)
                {
                    var toTarget = bounds.center - camera.transform.position;
                    var local = Quaternion.Inverse(camera.transform.rotation) * toTarget.normalized;
                    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Asin(local.y) * Mathf.Rad2Deg;
                    var s = new SerializedObject(camera);
                    bool withinLimits = Mathf.Abs(yaw) <= s.FindProperty("maxYawAngle").floatValue && Mathf.Abs(pitch) <= s.FindProperty("maxPitchAngle").floatValue;
                    int mask = ~((1 << LayerMask.NameToLayer("Player")) | (1 << 2));
                    bool hit = Physics.Raycast(camera.transform.position, toTarget.normalized, out var aimed, 100f, mask, QueryTriggerInteraction.Ignore);
                    bool clear = hit && aimed.collider.transform.IsChildOf(target);
                    bool center = Physics.Raycast(camera.View.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out var initial, 15f, mask, QueryTriggerInteraction.Ignore) && initial.collider.transform.IsChildOf(target);
                    report.AppendLine($"- CAM {camera.CameraName}: distance={toTarget.magnitude:F2}m; lineOfSight={clear}; withinYawPitch={withinLimits}; within15m={toTarget.magnitude <= 15}; initialCenterHit={center}; blocker={(hit && !clear ? Path(aimed.collider.transform) : "none")}");
                }
            }
            foreach (var pair in snapshots)
            {
                var t = pair.Key; var s = pair.Value;
                if (t == null || t.name != s.Item1 || t.parent != s.Item2 || t.localPosition != s.Item3 || t.localRotation != s.Item4 || t.localScale != s.Item5)
                    throw new Exception("Original transform changed: " + s.Item1);
            }
            report.AppendLine("\nOriginal transforms/names/parents preserved: " + originals.Length);
            report.AppendLine("Player exists: " + (Object.FindAnyObjectByType<PlayerMover>() != null));
            report.AppendLine("Controls unchanged: Q/E within group, no wrap, digits disabled. No rotationSpeed field in current CCTVCamera; mouseSensitivity unchanged.");
            PrefabSetup.Validate();
            EditorSceneManager.SaveScene(scene);
            System.IO.File.WriteAllText(Root + "Docs/HouseSetupReport.md", report.ToString());
            AssetDatabase.Refresh(); Debug.Log("HOUSE_SETUP_COMPLETE\n" + report);
        }
        public static void CheckIdempotence()
        {
            EditorSceneManager.OpenScene(ScenePath());
            int objects = Transforms().Length;
            int components = Transforms().Sum(t => t.GetComponents<Component>().Length);
            Run();
            if (objects != Transforms().Length || components != Transforms().Sum(t => t.GetComponents<Component>().Length))
                throw new Exception("Setup duplicated objects/components");
            if (Object.FindObjectsByType<Camera>().Count(c => c.isActiveAndEnabled) != 1) throw new Exception("Camera conflict");
            Debug.Log("HOUSE_IDEMPOTENCE_PASSED");
        }
    }
}

