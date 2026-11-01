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
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseEndingCheck
    {
        private static int phase,frame,endingLogs;private static double started;
        private static EndingSequence sequence;private static Keyboard keyboard;
        private static T F<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void R(bool condition,string message){if(!condition)throw new Exception(message);}
        static HouseEndingCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("EndingCheck",false)){started=EditorApplication.timeSinceStartup;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}};}
        private static void Log(string text,string stack,LogType type){if(text=="концовка 1")endingLogs++;}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("EndingCheck",true);EditorApplication.EnterPlaymode();}
        private static void Aim(GameObject target)
        {
            var bounds=target.GetComponentInChildren<Collider>().bounds;var camera=GameContext.Instance.CameraManager.ActiveCamera.transform;
            camera.position=bounds.center+Vector3.right*(bounds.extents.x+.5f);camera.LookAt(bounds.center);Physics.SyncTransforms();frame=Time.frameCount;
        }
        private static void StartCircuit(CurrentFlow flow)
        {typeof(CurrentFlow).GetField("speed",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(flow,40f);flow.SetPowered(true);flow.ActivateFuse();flow.Begin();}
        private static void Tick()
        {
            if(Time.frameCount<8||Time.frameCount<frame+4)return;
            try
            {
                if(EditorApplication.timeSinceStartup-started>50)throw new Exception("Ending timeout phase="+phase+" player="+GameContext.Instance.PlayerMover.transform.position);
                if(sequence==null)sequence=Object.FindAnyObjectByType<EndingSequence>();
                var opener=F<DoorOpener>(sequence,"laptopDoor");var first=F<InteractableSphere>(sequence,"firstLaptop");var entry=F<BoardController>(sequence,"entry");var last=F<BoardController>(sequence,"finalLaptop");
                if(phase==0)
                {
                    R(!opener.IsOpen&&!first.IsAvailable,"Door initially unlocked");StartCircuit(F<CurrentFlow>(sequence,"firstCircuit"));phase=1;return;
                }
                if(phase==1)
                {
                    if(!F<CurrentFlow>(sequence,"firstCircuit").IsFinished)return;
                    R(!opener.IsOpen&&!first.IsAvailable,"One circuit opened door");StartCircuit(F<CurrentFlow>(sequence,"secondCircuit"));phase=2;return;
                }
                if(phase==2)
                {
                    if(!F<CurrentFlow>(sequence,"secondCircuit").IsFinished)return;
                    R(opener.IsOpen&&first.IsAvailable,"Both circuits did not open door");Debug.Log("ENDING_TWO_CIRCUITS_GATE_PASSED");
                    foreach(var door in Object.FindObjectsByType<DoorOpener>())door.Open();
                    first.Complete();R(!F<GameObject>(sequence,"secretDoor").activeSelf,"Sek_door still active");
                    InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();
                    Aim(F<GameObject>(entry,"standingBoard"));frame=Time.frameCount+20;phase=3;return;
                }
                if(phase==3)
                {
                    R(entry.IsAimed,"Entry not aimed hit="+GameContext.Instance.AimProbe.Collider);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));frame=Time.frameCount;phase=4;return;
                }
                if(phase==4)
                {
                    if(!F<bool>(sequence,"entered"))return;
                    R(GameContext.Instance.CameraManager.ActiveCamera.CameraName=="31","Wrong room camera");
                    R(Vector3.Distance(GameContext.Instance.PlayerMover.transform.position,F<Transform>(sequence,"destination").position)<1f,"Wrong teleport point");
                    Debug.Log("ENDING_ENTRY_APPROACH_TELEPORT_CAMERA31_PASSED");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());Aim(F<GameObject>(last,"standingBoard"));phase=5;return;
                }
                if(phase==5)
                {
                    R(last.IsAimed,"Final laptop not aimed");R(endingLogs==0,"Early ending");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));frame=Time.frameCount;phase=6;return;
                }
                if(phase==6)
                {
                    if(endingLogs==0)return;
                    sequence.Finish();R(endingLogs==1,"Repeated ending log");Debug.Log("ENDING_FINAL_LAPTOP_APPROACH_LOG_ONCE_PASSED");Finish(0);
                }
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int result){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool("EndingCheck",false);EditorApplication.Exit(result);}
    }
}
