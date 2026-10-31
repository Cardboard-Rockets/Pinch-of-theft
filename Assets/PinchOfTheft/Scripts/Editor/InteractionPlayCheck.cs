using System.Linq;
using PinchOfTheft.CCTV;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PinchOfTheft.Interaction.Editor
{
    [InitializeOnLoad]
    public static class InteractionPlayCheck
    {
        static InteractionPlayCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("InteractionCheck", false))
                    EditorApplication.delayCall += Check;
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            SessionState.SetBool("InteractionCheck", true);
            EditorApplication.EnterPlaymode();
        }
        private static void Check()
        {
            try
            {
                var manager = Object.FindAnyObjectByType<CameraManager>();
                Require(manager.ActiveCamera.CameraName == "01");
                Require(manager.Cameras.Where(c => c.Group > 0).All(c => !c.isAccessible && !c.IsVisibleInMenu));
                var initial = manager.ActiveCamera;
                Physics.SyncTransforms();
                for (int group = 0; group < 2; group++)
                {
                    var camera = manager.Cameras.First(c => c.Group == group && c.Slot == 1);
                    Require(Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 15, ~0, QueryTriggerInteraction.Ignore));
                    Debug.Log("VISIBILITY: " + group + " hit " + hit.collider.name + " distance " + hit.distance);
                    var sphere = hit.collider.GetComponent<InteractableSphere>();
                    Require(sphere != null && sphere.IsAvailable);
                    sphere.Complete(); sphere.Complete();
                    Require(!sphere.IsAvailable && sphere.GetComponent<Collider>().enabled);
                    Require(!sphere.GetComponentInChildren<KeyPrompt>(true).gameObject.activeSelf);
                    Require(manager.Cameras.Where(c => c.Group == group + 1).All(c => c.isAccessible && c.IsVisibleInMenu));
                    Require(manager.ActiveCamera == initial);
                }
                Require(Object.FindObjectsByType<AudioListener>().Count(a => a.isActiveAndEnabled) == 1);
                Debug.Log("INTERACTION_PLAY_PASSED");
                SessionState.SetBool("InteractionCheck", false); EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool("InteractionCheck", false); EditorApplication.Exit(1);
            }
        }
        private static void Require(bool condition)
        {
            if (!condition) throw new System.Exception("Interaction check failed");
        }
    }
}

