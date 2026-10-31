using System;
using System.Linq;
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class FakeUnlockSetup
    {
        static FakeUnlockSetup()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("FakeUnlockCheck", false))
                    EditorApplication.delayCall += Check;
            };
        }
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var candidates = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.Equals("facke", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length != 1) throw new Exception("facke candidates: " + candidates.Length);
            var target = candidates[0];
            var manager = Object.FindAnyObjectByType<CameraManager>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab");
            if (prefab == null || manager == null) throw new Exception("Core/KeyPrompt missing");
            if (target.GetComponentsInChildren<Collider>(true).Length == 0) target.gameObject.AddComponent<BoxCollider>();
            target.gameObject.layer = LayerMask.NameToLayer("Interactable");
            var prompt = target.GetComponentInChildren<KeyPrompt>(true);
            if (prompt == null)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target);
                var scale = target.lossyScale;
                root.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                root.transform.rotation = Quaternion.identity;
                var bounds = target.GetComponentsInChildren<Collider>(true)[0].bounds;
                root.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.3f);
                prompt = root.GetComponentInChildren<KeyPrompt>(true);
            }
            var interactable = target.GetComponent<InteractableSphere>();
            if (interactable == null) interactable = target.gameObject.AddComponent<InteractableSphere>();
            var settings = new SerializedObject(interactable);
            settings.FindProperty("unlockGroup").intValue = -1;
            settings.FindProperty("singleUse").boolValue = false;
            settings.FindProperty("startAvailable").boolValue = true;
            settings.FindProperty("holdDuration").floatValue = 2f;
            settings.FindProperty("cameraManager").objectReferenceValue = manager;
            settings.FindProperty("prompt").objectReferenceValue = prompt;
            settings.FindProperty("onCompleted").FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SessionState.SetBool("FakeUnlockCheck", true);
            EditorApplication.EnterPlaymode();
        }
        private static void Check()
        {
            try
            {
                var manager = GameContext.Instance.CameraManager;
                var target = Object.FindObjectsByType<InteractableSphere>().Single(t => t.name.Equals("facke", StringComparison.OrdinalIgnoreCase));
                var cameras = manager.Cameras.ToArray();
                var accessible = cameras.Select(c => c.isAccessible).ToArray();
                var visible = cameras.Select(c => c.IsVisibleInMenu).ToArray();
                var active = manager.ActiveCamera;
                target.SetVisual(0.5f, true);
                if (target.GetComponentInChildren<CanvasGroup>(true).alpha < 0.99f) throw new Exception("Fake prompt not highlighted");
                target.Complete(); target.Complete();
                if (!target.IsAvailable || manager.ActiveCamera != active) throw new Exception("Fake changed availability/camera");
                for (int i = 0; i < cameras.Length; i++)
                    if (cameras[i].isAccessible != accessible[i] || cameras[i].IsVisibleInMenu != visible[i]) throw new Exception("Fake unlocked cameras");
                Debug.Log("FAKE_UNLOCK_PLAY_PASSED: repeatable interaction, prompt, no camera/group changes");
                SessionState.SetBool("FakeUnlockCheck", false); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool("FakeUnlockCheck", false); EditorApplication.Exit(1); }
        }
    }
}
