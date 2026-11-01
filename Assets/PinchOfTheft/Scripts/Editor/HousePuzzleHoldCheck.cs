using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HousePuzzleHoldCheck
    {
        private static int phase,frame;
        private static float heldAt;
        private static double began;
        private static Keyboard keyboard;
        private static InteractableSphere target;
        static HousePuzzleHoldCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("PuzzleHoldCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("PuzzleHoldCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>35)throw new Exception("Hold timeout phase="+phase);
                if(Time.frameCount<5||Time.frameCount<frame+3)return;
                var context=GameContext.Instance;var manager=context.CameraManager;
                if(phase==0||phase==3)
                {
                    if(phase==0){manager.UnlockGroup(1);InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();}
                    var camera=manager.Cameras.Single(c=>c.CameraName==(phase==0?"11":"12"));manager.SelectCamera(camera);
                    target=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name==(phase==0?"onlock tv|panel":"keypad"));
                    var local=Quaternion.Inverse(camera.transform.rotation)*(target.GetComponent<Collider>().bounds.center-camera.transform.position).normalized;
                    camera.Rotate(new Vector2(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,Mathf.Asin(local.y)*Mathf.Rad2Deg));InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;phase++;
                }
                else if(phase==1||phase==4)
                {
                    if(!Object.FindAnyObjectByType<SphereInteractor>().HasTarget)throw new Exception("No target "+target.name);
                    if(target.GetComponentInChildren<CanvasGroup>(true).alpha<0.99f)throw new Exception("No prompt "+target.name);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));heldAt=Time.time;frame=Time.frameCount;phase++;
                }
                else if(phase==2)
                {
                    if(Time.time-heldAt<2.3f)return;
                    if(target.IsAvailable||manager.ActiveCamera.CameraName!="11"||!Object.FindAnyObjectByType<CodePuzzle>().CanOpen)throw new Exception("TV hold failed");
                    Debug.Log("TV_HOLD_PASSED: CAM11, 2 seconds, prompt, E suppressed");InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;phase=3;
                }
                else if(phase==5)
                {
                    if(Time.time-heldAt<0.8f)return;
                    var keypad=Object.FindAnyObjectByType<KeypadUI>();if(!keypad.IsOpen||!context.InputLock.IsLocked||manager.ActiveCamera.CameraName!="12")throw new Exception("Panel hold failed");
                    keypad.Close();Debug.Log("PANEL_HOLD_PASSED: CAM12, 0.5 seconds, prompt, input lock");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("PuzzleHoldCheck",false);EditorApplication.Exit(code);}
    }
}
