using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    public static class HouseUnlockChainSetup
    {
        private static void Set(Object o, string field, object value)
        {
            var data = new SerializedObject(o); var p = data.FindProperty(field);
            if (value is bool b) p.boolValue = b;
            else if (value is float f) p.floatValue = f;
            else if (value is int i) p.intValue = i;
            else p.objectReferenceValue = value as Object;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static UnityEvent Event(Object o, string field) => (UnityEvent)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        private static void Wire(Object o, string field, UnityAction action)
        {
            var e = Event(o, field);
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == action.Target as Object && e.GetPersistentMethodName(i) == action.Method.Name) return;
            UnityEventTools.AddPersistentListener(e, action); EditorUtility.SetDirty(o);
        }
        private static void Available(Object o, string field, InteractableSphere target)
        {
            var e = Event(o, field);
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == "SetAvailable") return;
            UnityEventTools.AddBoolPersistentListener(e, target.SetAvailable, true); EditorUtility.SetDirty(o);
        }
        private static T Add<T>(GameObject o) where T : Component
        { var c = o.GetComponent<T>(); if (c == null) c = o.AddComponent<T>(); return c; }
        private static string Normalize(string n) => new string(n.ToLowerInvariant().Where(c => c != ' ' && c != '_').ToArray());
        private static Transform Find(Transform[] all, params string[] names) => all.Single(t => names.Contains(Normalize(t.name)));

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var ctx = Object.FindAnyObjectByType<GameContext>();
            if (ctx == null) throw new Exception("Core отсутствует");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var router = Find(all, "router"); var onlocker = Find(all, "onlocker", "onlockonlocker", "unlocker");
            var unlock = Find(all, "unlockcam21", "onlockcam21");
            var box = Object.FindObjectsByType<BoardController>().Single(c =>
                ((GameObject)new SerializedObject(c).FindProperty("standingBoard").objectReferenceValue).name == "Box_A1");
            var candidates = Object.FindObjectsByType<DoorOpener>().Where(d => !d.transform.parent.name.Equals("Door_code", StringComparison.OrdinalIgnoreCase) &&
                !d.transform.parent.name.Equals("Boxx", StringComparison.OrdinalIgnoreCase)).OrderBy(d => Vector3.Distance(d.transform.position, onlocker.position)).ToArray();
            if (candidates.Length == 0 || (candidates.Length > 1 && Vector3.Distance(candidates[1].transform.position, onlocker.position) - Vector3.Distance(candidates[0].transform.position, onlocker.position) < 0.5f))
                throw new Exception("Неоднозначная дверь цепочки");
            var door = candidates[0];
            Debug.Log("CHAIN_SELECTED_DOOR " + door.transform.parent.name + "/" + door.name);
            var doorController = Object.FindObjectsByType<DoorController>().Single(c =>
                new SerializedObject(c).FindProperty("closedDoor").objectReferenceValue == door.gameObject);
            doorController.enabled = true;
            var puzzle = Object.FindObjectsByType<CodePuzzle>().Single();
            var old = Event(puzzle, "onCodeGenerated");
            for (int i = old.GetPersistentEventCount() - 1; i >= 0; i--)
                if (old.GetPersistentTarget(i) == doorController) UnityEventTools.RemovePersistentListener(old, i);
            EditorUtility.SetDirty(puzzle);
            var r = Configure(router, 2f, ctx);
            var d = door.GetComponent<InteractableSphere>();
            var routerEvent = Event(r, "onCompleted");
            for (int i = routerEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                if (d != null && routerEvent.GetPersistentTarget(i) == d) UnityEventTools.RemovePersistentListener(routerEvent, i);
            if (d != null) Object.DestroyImmediate(d);
            var oldPrompt = door.GetComponentInChildren<KeyPrompt>(true);
            if (oldPrompt != null) oldPrompt.gameObject.SetActive(false);
            Wire(r, "onCompleted", doorController.Unlock);
            var popup = (GameObject)new SerializedObject(doorController).FindProperty("popup").objectReferenceValue;
            var bounds = door.GetComponent<Renderer>().bounds;
            popup.transform.position = bounds.center + Vector3.up * .35f;
            var popupScale = popup.transform.parent.lossyScale;
            popup.transform.localScale = new Vector3(.003f / popupScale.x, .003f / popupScale.y, .003f / popupScale.z);
            var rect = popup.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(300, 70);
            var caption = popup.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (caption != null) { caption.fontSize = 32; caption.raycastTarget = false; }
            popup.SetActive(false);
            var o = Configure(onlocker, 2f, ctx);
            var u = unlock.GetComponent<InteractableSphere>();
            if (u == null) throw new Exception("Unlock cam21 без Interactable");
            Set(u, "startAvailable", false); Set(u, "preventReactivation", true);
            Available(box, "onFallen", r);
            Available(door, "onOpened", o);
            if (!all.Any(t => t.name == "WireRepair")) Available(o, "onCompleted", u);
            var reveal = Add<RevealOnEvent>(router.gameObject); Set(reveal, "target", router.gameObject);
            Wire(box, "onFallen", reveal.Show);
            var hint = Add<LockedHint>(unlock.gameObject); Set(hint, "target", u);
            var label = unlock.Find("LockedHint");
            if (label == null)
            {
                var flow = Object.FindObjectsByType<CurrentFlow>().First(f => f.name == "wire1");
                var source = (GameObject)new SerializedObject(flow).FindProperty("noPowerLabel").objectReferenceValue;
                label = Object.Instantiate(source, unlock).transform; label.name = "LockedHint";
                label.position = unlock.position + Vector3.up * 0.3f;
                var scale = unlock.lossyScale; label.localScale = new Vector3(.004f / scale.x, .004f / scale.y, .004f / scale.z);
            }
            label.gameObject.SetActive(false); Set(hint, "label", label.gameObject);
            var damek = Find(all, "damek");
            if (damek.Find("VisualFacing") == null)
            {
                var visual = new GameObject("VisualFacing").transform; visual.SetParent(damek, false);
                foreach (var child in new[] { damek.Find("Body"), damek.Find("Wheel") })
                    if (child != null) child.SetParent(visual, true);
                visual.localRotation = Quaternion.Euler(0, 90, 0);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            PrefabSetup.Validate(); Debug.Log("HOUSE_UNLOCK_CHAIN_SETUP_PASSED");
        }

        public static void Audit()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var all = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var router = Find(all, "router"); var onlocker = Find(all, "onlockonlocker");
            Find(all, "boxa1").gameObject.SetActive(false);
            Find(all, "boxb").gameObject.SetActive(true);
            Physics.SyncTransforms();
            foreach (var target in new[] { router, onlocker })
            {
                var center = target.GetComponent<Collider>().bounds.center;
                foreach (var camera in Object.FindObjectsByType<PinchOfTheft.CCTV.CCTVCamera>().Where(c => c.Group <= 1))
                {
                    var direction = center - camera.transform.position;
                    var aimRay = camera.View.ViewportPointToRay(camera.View.WorldToViewportPoint(center));
                    bool clear = Physics.Raycast(aimRay, out var hit, direction.magnitude + .1f,
                        ~((1 << LayerMask.NameToLayer("Player")) | (1 << 2)), QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(target);
                    var viewport = camera.View.WorldToViewportPoint(center);
                    bool inView = viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1;
                    Debug.Log("CHAIN_VISIBILITY " + target.name + " camera=" + camera.CameraName + " distance=" + direction.magnitude + " clear=" + clear + " initialView=" + inView + " firstHit=" + hit.transform?.name);
                }
            }
            var ray = onlocker.position - router.position;
            foreach (var hit in Physics.RaycastAll(router.position, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore))
                Debug.Log("CHAIN_BETWEEN " + hit.transform.name + " parent=" + hit.transform.parent?.name);
            PrefabSetup.Validate();
        }

        private static InteractableSphere Configure(Transform t, float duration, GameContext ctx)
        {
            var interact = Add<InteractableSphere>(t.gameObject);
            Set(interact, "unlockGroup", -1); Set(interact, "singleUse", true); Set(interact, "startAvailable", false);
            Set(interact, "holdDuration", duration); Set(interact, "preventReactivation", true); Set(interact, "cameraManager", ctx.CameraManager);
            t.gameObject.layer = LayerMask.NameToLayer("Interactable");
            if (t.GetComponentsInChildren<Collider>(true).Length == 0) t.gameObject.AddComponent<BoxCollider>();
            var prompt = t.GetComponentInChildren<KeyPrompt>(true);
            if (prompt == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, t); root.name = "KeyPrompt";
                root.transform.position = t.GetComponent<Renderer>().bounds.center + Vector3.up * .3f;
                var s = t.lossyScale; root.transform.localScale = new Vector3(1 / s.x, 1 / s.y, 1 / s.z);
                prompt = root.GetComponentInChildren<KeyPrompt>(true);
            }
            Set(interact, "prompt", prompt);
            return interact;
        }
    }
}
