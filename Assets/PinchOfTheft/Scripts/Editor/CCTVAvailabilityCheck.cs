using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.CCTV.Editor
{
    [InitializeOnLoad]
    public static class CCTVAvailabilityCheck
    {
        static CCTVAvailabilityCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CCTV.Availability", false))
                    EditorApplication.delayCall += Check;
            };
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            SessionState.SetBool("CCTV.Availability", true);
            EditorApplication.EnterPlaymode();
        }

        private static void Check()
        {
            try
            {
                var manager = UnityEngine.Object.FindAnyObjectByType<CameraManager>();
                var menu = UnityEngine.Object.FindAnyObjectByType<CameraMenuUI>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                Action refresh = () => typeof(CameraManager).GetMethod("EnsureAvailableCamera", flags).Invoke(manager, null);
                Func<string, CCTVCamera> camera = name => manager.Cameras.Single(c => c.CameraName == name);
                var label = (Text)typeof(CameraMenuUI).GetField("cameraLabel", flags).GetValue(menu);
                Action<int> cycle = direction => typeof(CameraManager).GetMethod("SwitchCamera", flags).Invoke(manager, new object[] { direction });
                Action<string> active = name =>
                {
                    Require(label.text == "CAM " + name, "Camera label " + name);
                    Require(manager.ActiveCamera != null && manager.ActiveCamera.CameraName == name, name);
                    Require(manager.Cameras.Count(c => c.GetComponent<AudioListener>().enabled) == 1, "One listener");
                };
                camera("23").SetAccessible(false);
                typeof(CCTVCamera).GetField("isVisibleInMenu", flags).SetValue(camera("24"), false);
                active("01");
                // Reverse Inspector order to prove traversal uses group/slot.
                typeof(CameraManager).GetField("cameras", flags).SetValue(manager, manager.Cameras.Reverse().ToArray());
                foreach (string name in new[] { "02", "03", "04", "04" })
                { cycle(1); active(name); }
                foreach (string name in new[] { "03", "02", "01", "01" })
                { cycle(-1); active(name); }
                manager.SelectCamera(camera("22")); cycle(1); active("24");
                cycle(1); active("24"); cycle(-1); active("22"); cycle(-1); active("21"); cycle(-1); active("21");
                manager.SelectCamera(camera("01"));
                camera("02").SetAccessible(false);
                cycle(1); active("03"); cycle(-1); active("01");
                camera("02").SetAccessible(true);
                foreach (string name in new[] { "02", "03", "04", "11", "12" })
                { manager.SelectCamera(camera(name)); active(name); }
                manager.SelectCamera(camera("23")); active("12");
                menu.SetOpen(true);
                cycle(1); active("12"); cycle(-1); active("12");
                Require(!label.gameObject.activeSelf, "Label hidden in menu");
                typeof(CameraMenuUI).GetMethod("LateUpdate", flags).Invoke(menu, null);
                var buttons = menu.GetComponentsInChildren<Button>();
                Require(!buttons.Single(b => b.name == "Select_23").interactable, "Disabled button");
                Require(!buttons.Any(b => b.name == "Select_24"), "Hidden camera");
                var crosshair = (GameObject)typeof(CameraMenuUI).GetField("crosshair", flags).GetValue(menu);
                Require(!crosshair.activeSelf && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Open menu");
                buttons.Single(b => b.name == "Select_11").onClick.Invoke(); active("11");
                Require(label.gameObject.activeSelf, "Label visible after selection");
                manager.SelectCamera(camera("24")); active("24");
                Require(crosshair.activeSelf && !menu.IsOpen, "Closed menu");
                Debug.Log("CCTV_CURSOR_OBSERVED: " + Cursor.lockState + ", visible=" + Cursor.visible);
                camera("24").SetAccessible(false); refresh(); active("21");
                foreach (var c in manager.Cameras.Where(c => c.Group == 2)) c.SetAccessible(false);
                refresh(); active("01");
                foreach (var c in manager.Cameras) c.SetAccessible(false);
                refresh(); Require(manager.ActiveCamera == null, "No available camera");
                Require(label.text == "NO SIGNAL", "No signal label");
                Require(manager.Cameras.All(c => !c.GetComponent<AudioListener>().enabled && !c.GetComponent<Camera>().enabled), "All views disabled");
                camera("12").SetAccessible(true); refresh(); active("12");
                cycle(1); active("12"); cycle(-1); active("12");
                typeof(CameraManager).GetMethod("Start", flags).Invoke(manager, null); active("12");
                Debug.Log("CCTV_AVAILABILITY_PLAY_PASSED");
                SessionState.SetBool("CCTV.Availability", false);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetBool("CCTV.Availability", false);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new Exception("CCTV check failed: " + message);
        }
    }
}





