using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class DamekInputCheck
    {
        private static Mouse mouse;
        private static int phase,frame;
        private static Vector3 inside,destination;
        private static double began;
        static DamekInputCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DamekInput",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("DamekInput",true);EditorApplication.EnterPlaymode();}
        private static void R(bool v,string m){if(!v)throw new Exception(m);}
        private static void Aim(Vector3 point){var camera=GameContext.Instance.CameraManager.ActiveCamera.transform;camera.position=point+Vector3.up*0.9f;camera.LookAt(point);Physics.SyncTransforms();}
        private static void Click(){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));frame=Time.frameCount;}
        private static void Release(){InputSystem.QueueStateEvent(mouse,new MouseState());frame=Time.frameCount;}
        private static void Tick()
        {
            if(Time.frameCount<5||Time.frameCount<frame+3)return;
            try
            {
                if(EditorApplication.timeSinceStartup-began>45)throw new Exception("Damek input timeout phase="+phase);
                var c=GameContext.Instance;var mover=c.PlayerMover;
                if(phase==0){InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;mouse=InputSystem.AddDevice<Mouse>();R(NavMesh.SamplePosition(mover.transform.position,out var hit,1,NavMesh.AllAreas),"No start nav point");inside=hit.position;Aim(new Vector3(11.25f,1.15f,-25.2f));frame=Time.frameCount;phase=1;}
                else if(phase==1){Click();phase=2;}
                else if(phase==2){R(mover.IsMoving&&c.Marker.gameObject.activeSelf,"RMB did not move/show marker");destination=mover.Destination;Debug.Log("DAMEK_RMB_START_PASSED");Release();Aim(inside);phase=3;}
                else if(phase==3){Click();phase=4;}
                else if(phase==4){R(Vector3.Distance(destination,mover.Destination)>1f,"RMB redirect failed");destination=mover.Destination;Debug.Log("DAMEK_RMB_REDIRECT_PASSED");Release();c.Menu.SetOpen(true);Aim(new Vector3(11.25f,1.15f,-25.2f));phase=5;}
                else if(phase==5){Click();phase=6;}
                else if(phase==6){R(Vector3.Distance(destination,mover.Destination)<0.01f,"RMB not blocked by menu");c.Menu.SetOpen(false);Release();phase=7;}
                else if(phase==7&&!mover.IsMoving){R(!c.Marker.gameObject.activeSelf,"Marker did not hide");Debug.Log("DAMEK_RMB_MENU_AND_MARKER_PASSED");Finish(0);}
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("DamekInput",false);EditorApplication.Exit(code);}
    }
}
