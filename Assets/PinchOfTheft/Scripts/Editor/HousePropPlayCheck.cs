using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HousePropPlayCheck
    {
        private static BoardController board;
        private static Keyboard keyboard;
        private static int phase, frame;
        private static double started;

        static HousePropPlayCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("HousePropPlayCheck", false)) return;
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            };
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            SessionState.SetBool("HousePropPlayCheck", true);
            EditorApplication.EnterPlaymode();
        }

        private static T Field<T>(object source, string name) =>
            (T)source.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(source);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Tick()
        {
            if (Time.frameCount < 8 || Time.frameCount < frame + 3) return;
            try
            {
                if (EditorApplication.timeSinceStartup - started > 25)
                    throw new Exception("Board timeout phase=" + phase + " player=" + GameContext.Instance.PlayerMover.transform.position);
                if (phase == 0)
                {
                    board = Object.FindObjectsByType<BoardController>().Single(c => Field<GameObject>(c, "standingBoard").name == "board");
                    var standing = Field<GameObject>(board, "standingBoard");
                    var fallen = Field<GameObject>(board, "fallenBoard");
                    Require(standing.activeSelf && !fallen.activeSelf, "Initial board state");
                    InputSystem.settings = Object.Instantiate(InputSystem.settings);
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    var bounds = standing.GetComponent<Renderer>().bounds;
                    var camera = GameContext.Instance.CameraManager.ActiveCamera.transform;
                    camera.position = bounds.center + Vector3.forward * 1.1f;
                    camera.LookAt(bounds.center);
                    Physics.SyncTransforms();
                    phase = 1;
                    frame = Time.frameCount;
                }
                else if (phase == 1)
                {
                    Require(board.IsAimed, "Board was not aimed");
                    Require(Field<GameObject>(board, "popup").activeSelf, "Board popup hidden");
                    Require(Field<GameObject>(board, "popup").GetComponentInChildren<Text>().text.Contains("Уронить"), "Popup label");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    frame = Time.frameCount;
                    phase = 2;
                }
                else if (phase == 2)
                {
                    var standing = Field<GameObject>(board, "standingBoard");
                    var fallen = Field<GameObject>(board, "fallenBoard");
                    if (standing.activeSelf)
                    {
                        Require(!fallen.activeSelf, "Fallen model appeared before arrival");
                        return;
                    }
                    Require(fallen.activeSelf && !board.enabled && !board.IsAimed, "Board did not finish cleanly");
                    var player = GameContext.Instance.PlayerMover.transform.position;
                    var goal = Field<Transform>(board, "approachPoint").position;
                    var delta = player - goal; delta.y = 0;
                    Require(delta.magnitude <= 0.4f, "Board fell before Damek arrived");
                    Debug.Log("HOUSE_BOARD_E_APPROACH_SWAP_PASSED distance=" + delta.magnitude);
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("HousePropPlayCheck", false);
            EditorApplication.Exit(code);
        }
    }
}
