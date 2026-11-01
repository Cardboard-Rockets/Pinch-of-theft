using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseUnlockChainCheck
    {
        private static int phase, frame;
        private static InteractableSphere router, onlocker, unlock;
        private static DoorController door;
        private static Keyboard keyboard;
        private static double started;
        static HouseUnlockChainCheck()
        {
            EditorApplication.playModeStateChanged += s =>
            { if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("UnlockChainCheck", false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; } };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity"); SessionState.SetBool("UnlockChainCheck", true); EditorApplication.EnterPlaymode();
        }
        private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        private static void Require(bool v, string message) { if (!v) throw new Exception(message); }
        private static void Tick()
        {
            if (Time.frameCount < 8 || Time.frameCount < frame + 3) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 35) throw new Exception("Chain door approach timeout phase=" + phase);
                if (phase == 0)
                {
                    var all = Object.FindObjectsByType<InteractableSphere>(FindObjectsInactive.Include);
                    router = all.Single(i => i.name == "router"); onlocker = all.Single(i => i.name == "onlock onlocker");
                    unlock = all.Single(i => i.name == "Unlock cam21");
                    door = Object.FindObjectsByType<DoorController>().Single(c => Field<GameObject>(c, "closedDoor").transform.parent.name == "Door (2)");
                    Require(!router.IsAvailable && !door.IsUnlocked && !onlocker.IsAvailable && !unlock.IsAvailable, "Initial sequence gates");
                    var camera = GameContext.Instance.CameraManager.ActiveCamera.transform;
                    var bounds = unlock.GetComponentInChildren<Collider>().bounds;
                    camera.position = bounds.center + Vector3.right * (bounds.extents.x + .25f); camera.LookAt(bounds.center); Physics.SyncTransforms();
                    frame = Time.frameCount; phase = 1; return;
                }
                if (phase == 1)
                {
                    Require(Field<GameObject>(unlock.GetComponent<LockedHint>(), "label").activeSelf, "Locked hint not shown; hit=" + GameContext.Instance.AimProbe.Collider);
                    var box = Object.FindObjectsByType<BoardController>().Single(c => Field<GameObject>(c, "standingBoard").name == "Box_A1");
                    Field<UnityEvent>(box, "onFallen").Invoke();
                    Require(router.IsAvailable && !door.IsUnlocked, "Box to router event");
                    var closed = Field<GameObject>(door, "closedDoor");
                    var bounds = closed.GetComponent<Collider>().bounds;
                    var camera = GameContext.Instance.CameraManager.ActiveCamera.transform;
                    var normal = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
                    camera.position = bounds.center + normal * .6f; camera.LookAt(bounds.center); Physics.SyncTransforms();
                    frame = Time.frameCount; phase = 2; return;
                }
                if (phase == 2)
                {
                    Require(door.IsAimed && Field<GameObject>(door,"popup").activeSelf, "Locked door popup missing");
                    Require(Field<GameObject>(door,"popup").GetComponentInChildren<Text>().text == "Закрыто", "Locked caption missing");
                    router.Complete(); Require(door.IsUnlocked && !onlocker.IsAvailable, "Router to door permission");
                    var closed = Field<GameObject>(door,"closedDoor");
                    foreach (var opener in Object.FindObjectsByType<DoorOpener>()) if (opener.gameObject != closed) opener.Open();
                    InputSystem.settings = Object.Instantiate(InputSystem.settings);
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    frame = Time.frameCount + 20; phase = 3; return;
                }
                if (phase == 3)
                {
                    Require(Field<GameObject>(door,"popup").GetComponentInChildren<Text>().text.Contains("Открыть"), "Open caption missing");
                    Require(Field<GameObject>(door,"closedDoor").activeSelf, "Door opened before approach");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));
                    frame = Time.frameCount; phase = 4; return;
                }
                if (phase == 4)
                {
                    if (Field<GameObject>(door,"closedDoor").activeSelf) return;
                    Require(Field<GameObject>(door,"openDoor").activeSelf && onlocker.IsAvailable, "Door to onlocker event");
                    onlocker.Complete();
                    var repair = Object.FindObjectsByType<BoardController>().FirstOrDefault(c => c.name == "WireRepair");
                    if (repair != null)
                    {
                        Require(!unlock.IsAvailable && Field<GameObject>(repair,"activeEffect").activeSelf, "Onlocker bypassed wire repair");
                        Debug.Log("UNLOCK_CHAIN_TO_WIRE_REPAIR_PASSED"); Finish(0); return;
                    }
                    Require(unlock.IsAvailable, "Onlocker to unlock event");
                    unlock.Complete(); Require(unlock.HasCompleted && !unlock.IsAvailable, "Unlock not consumed");
                    unlock.SetAvailable(true); Require(!unlock.IsAvailable, "Used unlock reactivated");
                    Require(GameContext.Instance.CameraManager.Cameras.Where(c => c.Group == 2).All(c => c.isAccessible), "Group 2 locked");
                    var visual = GameContext.Instance.PlayerMover.transform.Find("VisualFacing");
                    Require(visual != null && Quaternion.Angle(visual.localRotation, Quaternion.Euler(0,90,0)) < .01f, "Damek facing");
                    Debug.Log("UNLOCK_CHAIN_EVENTS_AND_DAMEK_FACING_PASSED");
                    Finish(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Finish(int code)
        { EditorApplication.update -= Tick; SessionState.SetBool("UnlockChainCheck", false); EditorApplication.Exit(code); }
    }
}
