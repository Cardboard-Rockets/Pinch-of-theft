using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseBoxReachabilityCheck
    {
        private static int phase, frame;
        private static double started;
        private static BoardController controller;
        private static Keyboard keyboard;
        static HouseBoxReachabilityCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("HouseBoxReachability", false))
                {
                    started = EditorApplication.timeSinceStartup;
                    EditorApplication.update += Tick;
                }
            };
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            SessionState.SetBool("HouseBoxReachability", true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (Time.frameCount < 8 || Time.frameCount < frame + 4) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 25) throw new Exception("Box test timeout phase=" + phase);
                if (phase >= 2) { Continue(); return; }
                if (phase == 0)
                {
                    foreach (var door in Object.FindObjectsByType<DoorOpener>())
                        if (door.GetComponent<Renderer>() != null && door.GetComponent<Renderer>().bounds.center.y < 3.8f)
                            door.Open();
                    frame = Time.frameCount + 30;
                    phase = 1;
                    return;
                }
                var all = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var box = all.Single(t => t.name == "Box_A1").GetComponent<Renderer>().bounds;
                var moved = all.Single(t => t.name == "Box_B").GetComponent<Renderer>().bounds;
                var player = GameContext.Instance.PlayerMover;
                var agent = player.GetComponent<NavMeshAgent>();
                var forbidden = moved;
                forbidden.Expand((agent.radius + 0.2f) * 2f);
                var navPath = new NavMeshPath();
                int valid = 0;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    float edge = Mathf.Abs(direction.x) * box.extents.x + Mathf.Abs(direction.z) * box.extents.z;
                    var candidate = new Vector3(box.center.x, box.min.y, box.center.z) + direction * (edge + agent.radius + 0.5f);
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas)) continue;
                    NavMesh.CalculatePath(player.transform.position, hit.position, NavMesh.AllAreas, navPath);
                    bool accepted = Mathf.Abs(hit.position.y - box.min.y) < 0.5f && !forbidden.Contains(hit.position) && navPath.status == NavMeshPathStatus.PathComplete;
                    if (accepted) valid++;
                    var corners = navPath.corners;
                    Debug.Log("BOX_AFTER_OPEN angle=" + i * 45 + " hit=" + hit.position.ToString("F3") +
                        " path=" + navPath.status + " end=" + (corners.Length > 0 ? corners[corners.Length - 1].ToString("F3") : "none") +
                        " forbidden=" + forbidden.Contains(hit.position) + " valid=" + accepted);
                }
                Debug.Log("BOX_AFTER_OPEN_VALID=" + valid);
                if (valid == 0) throw new Exception("No reachable box approach after opening doors");
                controller = Object.FindObjectsByType<BoardController>().Single(c =>
                    ((GameObject)typeof(BoardController).GetField("standingBoard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c)).name == "Box_A1");
                InputSystem.settings = Object.Instantiate(InputSystem.settings);
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keyboard = InputSystem.AddDevice<Keyboard>();
                var camera = GameContext.Instance.CameraManager.ActiveCamera.transform;
                camera.position = box.center + Vector3.back * 1.1f;
                camera.LookAt(box.center);
                Physics.SyncTransforms();
                frame = Time.frameCount;
                phase = 2;
                return;
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Continue()
        {
            var standing = (GameObject)typeof(BoardController).GetField("standingBoard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var moved = (GameObject)typeof(BoardController).GetField("fallenBoard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            if (phase == 2)
            {
                if (!controller.IsAimed) throw new Exception("Box_A1 is not aimed");
                var popup = (GameObject)typeof(BoardController).GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                if (!popup.activeSelf || !popup.GetComponentInChildren<Text>().text.Contains("Отодвинуть"))
                    throw new Exception("Box popup missing");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                frame = Time.frameCount;
                phase = 3;
            }
            else if (phase == 3)
            {
                if (standing.activeSelf)
                {
                    if (moved.activeSelf) throw new Exception("Box_B appeared before approach");
                    return;
                }
                if (!moved.activeSelf || controller.enabled) throw new Exception("Box did not swap cleanly");
                var point = (Transform)typeof(BoardController).GetField("approachPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var distance = GameContext.Instance.PlayerMover.transform.position - point.position;
                distance.y = 0;
                if (distance.magnitude > 0.4f) throw new Exception("Box swapped too early");
                Debug.Log("HOUSE_BOX_E_APPROACH_SWAP_PASSED distance=" + distance.magnitude);
                frame = Time.frameCount + 30;
                phase = 4;
            }
            else if (phase == 4)
            {
                var obstacle = moved.GetComponent<NavMeshObstacle>();
                if (obstacle == null || !obstacle.enabled || !obstacle.carving) throw new Exception("Box_B obstacle missing");
                var bounds = moved.GetComponent<Collider>().bounds;
                var navPath = new NavMeshPath();
                var left = bounds.center + Vector3.left * 1.5f;
                var right = bounds.center + Vector3.right * 1.5f;
                bool sampleA = NavMesh.SamplePosition(left, out var a, 1f, NavMesh.AllAreas);
                bool sampleB = NavMesh.SamplePosition(right, out var b, 1f, NavMesh.AllAreas);
                bool samples = sampleA && sampleB;
                if (!samples) throw new Exception("No NavMesh around Box_B");
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, navPath);
                bool inside = false;
                var corners = navPath.corners;
                for (int i = 1; i < corners.Length; i++)
                    for (int j = 0; j <= 20; j++)
                    {
                        var p = Vector3.Lerp(corners[i - 1], corners[i], j / 20f);
                        if (bounds.Contains(p)) inside = true;
                    }
                Debug.Log("BOX_B_PATH status=" + navPath.status + " corners=" + corners.Length + " entersBounds=" + inside);
                if (inside) throw new Exception("Path traverses Box_B collider");
                Finish(0);
            }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("HouseBoxReachability", false);
            EditorApplication.Exit(code);
        }
    }
}
