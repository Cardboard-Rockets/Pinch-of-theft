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
    public static class HouseCircuitHoldCheck
    {
        private static int phase,frame;
        private static double began;
        private static float heldAt;
        private static Keyboard keyboard;
        private static InteractableSphere target,start,elec,fuse;
        private static CurrentFlow flow;
        private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void Require(bool v,string m){if(!v)throw new Exception(m);}
        static HouseCircuitHoldCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CircuitHold",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("CircuitHold",true);EditorApplication.EnterPlaymode();}
        private static void Aim(InteractableSphere value)
        {
            target=value;var center=target.GetComponent<Collider>().bounds.center;var camera=GameContext.Instance.CameraManager.ActiveCamera;bool found=false;
            foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down})
            {
                Vector3 pos=center+direction*1.5f;
                if(!Physics.Raycast(pos,-direction,out var hit,2f,~((1<<LayerMask.NameToLayer("Player"))|(1<<2)),QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<InteractableSphere>()!=target)continue;
                camera.transform.position=pos;camera.transform.LookAt(center);found=true;break;
            }
            Require(found,"Cannot aim at "+target.name);Physics.SyncTransforms();frame=Time.frameCount;
        }
        private static void Press(){Require(Object.FindAnyObjectByType<SphereInteractor>().HasTarget,"Missing target "+target.name);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));heldAt=Time.time;frame=Time.frameCount;}
        private static void Release(){InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>75)throw new Exception("Circuit hold timeout phase="+phase);
                if(Time.frameCount<5||Time.frameCount<frame+3)return;
                if(phase==0)
                {
                    flow=Object.FindAnyObjectByType<CurrentFlow>();start=Field<InteractableSphere>(flow,"startInteractable");elec=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("elec",StringComparison.OrdinalIgnoreCase));fuse=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("fu1",StringComparison.OrdinalIgnoreCase));
                    InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();Aim(elec);phase=1;
                }
                else if(phase==1){Press();phase=2;}
                else if(phase==2&&Time.time-heldAt>2.3f){Require(start.IsAvailable&&!elec.IsAvailable,"elec hold failed");Debug.Log("ELEC_E_HOLD_PASSED");Release();Aim(start);phase=3;}
                else if(phase==3){Press();phase=4;}
                else if(phase==4&&Time.time-heldAt>0.6f){Require(flow.IsRunning,"Start hold failed");Release();phase=5;}
                else if(phase==5&&!flow.IsRunning){Require(!flow.IsFinished&&start.IsAvailable,"Fuse failure retry state");Aim(fuse);phase=6;}
                else if(phase==6){Press();phase=7;}
                else if(phase==7&&Time.time-heldAt>1.3f){Require(!fuse.IsAvailable&&Field<bool>(flow,"fuseActivated"),"fu1 hold failed");Debug.Log("FUSE_E_HOLD_PASSED");Release();Aim(start);phase=8;}
                else if(phase==8){Press();phase=9;}
                else if(phase==9&&Time.time-heldAt>0.6f){Release();phase=10;}
                else if(phase==10&&flow.IsFinished){Require(!start.IsAvailable,"Start available after finish");Debug.Log("START_E_HOLD_RETRY_FINISH_PASSED default speed=2");Finish(0);}
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("CircuitHold",false);EditorApplication.Exit(code);}
    }
}
