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
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class DoorPermissionCheck
    {
        private static DoorController normal,code;
        private static Keyboard keyboard;
        private static int phase,frame;
        private static double began;
        private static T F<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void R(bool v,string s){if(!v)throw new Exception(s);}
        static DoorPermissionCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DoorPermissionCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("DoorPermissionCheck",true);EditorApplication.EnterPlaymode();}
        private static void Aim(DoorController controller){var closed=F<GameObject>(controller,"closedDoor");var bounds=closed.GetComponent<Collider>().bounds;var normal=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;var camera=GameContext.Instance.CameraManager.ActiveCamera.transform;camera.position=bounds.center+normal*0.6f;camera.LookAt(bounds.center);Physics.SyncTransforms();frame=Time.frameCount;}
        private static void Tick()
        {
            if(Time.frameCount<8||Time.frameCount<frame+3)return;
            try
            {
                if(EditorApplication.timeSinceStartup-began>20)throw new Exception("Door permission timeout phase="+phase+" player="+GameContext.Instance.PlayerMover.transform.position+" pending="+F<bool>(normal,"pending")+" touch="+typeof(DoorController).GetMethod("IsTouchingDoor",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(normal,null)+" goal="+F<Vector3>(normal,"destination")+" actual="+GameContext.Instance.PlayerMover.Destination);
                var source=Object.FindObjectsByType<CodePuzzle>().Single();
                if(phase==0){var controllers=Object.FindObjectsByType<DoorController>();normal=controllers.Single(d=>F<GameObject>(d,"closedDoor").transform.parent.name=="Door_tuch");code=controllers.Single(d=>F<GameObject>(d,"closedDoor").transform.parent.name=="Door_code");R(!normal.IsUnlocked&&!code.IsUnlocked,"Initially unlocked");InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();Aim(normal);phase=1;}
                else if(phase==1){R(normal.IsAimed&&F<GameObject>(normal,"popup").GetComponentInChildren<Text>().text=="Закрыто","Locked popup missing");source.OnShieldActivated();R(normal.IsUnlocked&&!code.IsUnlocked&&F<GameObject>(normal,"closedDoor").activeSelf,"TV power opened door instead of unlocking");frame=Time.frameCount;phase=2;}
                else if(phase==2){R(F<GameObject>(normal,"popup").GetComponentInChildren<Text>().text.Contains("Открыть"),"Open popup missing");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));frame=Time.frameCount;phase=3;}
                else if(phase==3&&!F<GameObject>(normal,"closedDoor").activeSelf){R(F<GameObject>(normal,"openDoor").activeSelf,"Normal open visual missing");Debug.Log("NORMAL_DOOR_F_APPROACH_OPEN_PASSED");InputSystem.QueueStateEvent(keyboard,new KeyboardState());source.ActivateTV();R(source.CheckCode(source.Code),"Correct code failed");R(code.IsUnlocked&&F<GameObject>(code,"closedDoor").activeSelf,"Code auto-opened door");Aim(code);phase=4;}
                else if(phase==4){R(code.IsAimed,"Code door not aimed");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));frame=Time.frameCount;phase=5;}
                else if(phase==5&&!F<GameObject>(code,"closedDoor").activeSelf){R(F<GameObject>(code,"openDoor").activeSelf,"Code door open visual missing");Debug.Log("CODE_DOOR_UNLOCK_THEN_E_APPROACH_OPEN_PASSED");Finish(0);}
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int c){EditorApplication.update-=Tick;SessionState.SetBool("DoorPermissionCheck",false);EditorApplication.Exit(c);}
    }
}

