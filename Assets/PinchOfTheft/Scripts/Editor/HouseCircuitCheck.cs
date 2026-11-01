using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseCircuitCheck
    {
        private static CurrentFlow flow;
        private static InteractableSphere start,elec,fuse;
        private static Transform current;
        private static Quaternion rotation;
        private static Vector3 first;
        private static int phase,failures,finishes,frame;
        private static double began;
        private static T Field<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        private static void Set(Object obj,string field,object value){var s=new SerializedObject(obj);var p=s.FindProperty(field);if(value is float f)p.floatValue=f;else if(value is bool b)p.boolValue=b;else if(value is int i)p.intValue=i;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static HouseCircuitCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CircuitCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("CircuitCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>70)throw new Exception("Circuit timeout phase="+phase);
                if(Time.frameCount<5||Time.frameCount<frame+2)return;
                if(phase==0)
                {
                    flow=Object.FindObjectsByType<CurrentFlow>().Single(f=>f.name=="wire1");start=Field<InteractableSphere>(flow,"startInteractable");elec=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("elec",StringComparison.OrdinalIgnoreCase));fuse=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("fu1",StringComparison.OrdinalIgnoreCase));current=Field<Transform>(flow,"currentObject");rotation=current.rotation;
                    Require(!start.IsAvailable&&Field<GameObject>(flow,"noPowerLabel").activeSelf,"Start has power initially");flow.Begin();Require(!flow.IsRunning,"Begin without power");
                    Field<UnityEvent>(flow,"onFuseFailed").AddListener(()=>failures++);Field<UnityEvent>(flow,"onFinished").AddListener(()=>finishes++);
                    elec.Complete();Require(start.IsAvailable&&!Field<GameObject>(flow,"noPowerLabel").activeSelf&&!elec.IsAvailable,"elec event failed");Set(flow,"speed",20f);start.Complete();Require(flow.IsRunning,"Start did not begin");first=current.position;phase=1;
                }
                else if(phase==1&&!flow.IsRunning)
                {
                    Require(failures==1&&!flow.IsFinished&&start.IsAvailable&&!current.gameObject.activeSelf,"Return-to-start failure state");Require(Vector3.Distance(current.position,first)<0.001f,"Not returned to Start");Require(Mathf.Abs(Field<float>(flow,"traveled")-flow.FuseDistance)<0.02f,"Fuse crossing distance");Debug.Log("CIRCUIT_RETURN_FAILURE_PASSED");
                    Set(flow,"failMode",0);start.Complete();phase=2;
                }
                else if(phase==2&&!flow.IsRunning)
                {
                    Require(failures==2&&start.IsAvailable&&!current.gameObject.activeSelf,"Vanish state");Require(Vector3.Distance(current.position,Field<Vector3>(flow,"fusePosition"))<0.01f,"Vanish not at fuse");Debug.Log("CIRCUIT_VANISH_FAILURE_PASSED");
                    fuse.Complete();Require(Field<bool>(flow,"fuseActivated")&&!fuse.IsAvailable,"fu1 activation failed");start.Complete();phase=3;
                }
                else if(phase==3&&flow.IsFinished)
                {
                    Require(finishes==1&&!start.IsAvailable,"Finish event or Start state");var finish=Field<Transform>(flow,"finishPoint");Require(Vector3.Distance(current.position,finish.GetComponent<Renderer>().bounds.center)<0.01f,"Current not inside Finish");Require(Quaternion.Angle(current.rotation,rotation)<0.01f,"Current rotated");flow.Begin();Require(!flow.IsRunning,"Restart after finish");Debug.Log("CIRCUIT_RETRY_FINISH_PASSED");
                    var clone=new GameObject("Preactivated circuit test").AddComponent<CurrentFlow>();EditorUtility.CopySerialized(flow,clone);flow=clone;Set(flow,"fuseActivated",true);phase=4;
                }
                else if(phase==4)
                {
                    flow.Begin();Require(flow.IsRunning,"Preactivated start failed");phase=5;
                }
                else if(phase==5&&flow.IsFinished)
                {
                    Debug.Log("CIRCUIT_PREACTIVATED_PASSED");
                    var clone=new GameObject("Mid-flight circuit test").AddComponent<CurrentFlow>();EditorUtility.CopySerialized(flow,clone);flow=clone;Set(flow,"fuseActivated",false);flow.Begin();flow.ActivateFuse();phase=6;
                }
                else if(phase==6&&flow.IsFinished)
                {
                    Require(Object.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1,"Multiple listeners");Debug.Log("CIRCUIT_MIDFLIGHT_FUSE_PASSED");Debug.Log("HOUSE_CIRCUIT_PLAY_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("CircuitCheck",false);EditorApplication.Exit(code);}
    }
}


