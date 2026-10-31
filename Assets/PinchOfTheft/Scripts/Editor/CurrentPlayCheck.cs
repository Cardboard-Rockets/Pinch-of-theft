using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction.Editor
{
    [InitializeOnLoad]
    public static class CurrentPlayCheck
    {
        private static CurrentFlow flow;
        private static Transform current;
        private static Quaternion rotation;
        private static double started;
        private static int stage;
        private static int finishes;
        private static object Field(object obj,string field)=>obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        static CurrentPlayCheck(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CurrentCheck",false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");SessionState.SetBool("CurrentCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>60)throw new Exception("Current movement timeout");
                if(Time.frameCount<3)return;
                if(stage==0)
                {
                    flow=UnityEngine.Object.FindAnyObjectByType<CurrentFlow>();current=(Transform)Field(flow,"currentObject");rotation=current.rotation;
                    var wires=(Transform[])Field(flow,"wires");var old=wires[1].position;wires[1].position+=Vector3.up*50;flow.Begin();if(flow.IsRunning)throw new Exception("Broken chain started");wires[1].position=old;Physics.SyncTransforms();
                    ((UnityEvent)Field(flow,"onFinished")).AddListener(()=>finishes++);
                    var start=((Transform)Field(flow,"startPoint")).GetComponent<InteractableSphere>();start.Complete();
                    if(start.IsAvailable || !flow.IsRunning)throw new Exception("Start failed");flow.Begin();stage=1;
                }
                else if(stage==1)
                {
                    if(Quaternion.Angle(rotation,current.rotation)>0.01f)throw new Exception("Current rotation changed");
                    if(!flow.IsFinished)return;
                    var finish=(Transform)Field(flow,"finishPoint");var renderer=finish.GetComponent<Renderer>();
                    if(flow.IsRunning || finishes!=1 || Vector3.Distance(current.position,renderer.bounds.center)>0.001f)throw new Exception("Finish failed");
                    flow.Begin();if(flow.IsRunning || finishes!=1)throw new Exception("Repeated begin");
                    Debug.Log("CURRENT_PLAY_PASSED");Finish(0);
                }
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("CurrentCheck",false);EditorApplication.Exit(code);}
    }
}
