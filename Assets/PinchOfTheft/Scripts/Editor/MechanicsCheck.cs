using System;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class MechanicsCheck
    {
        private static BoardController board;
        private static CurrentFlow flow;
        private static double began;
        private static float phaseTime;
        private static int phase;
        private static object Field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        private static void Approach()=>typeof(BoardController).GetMethod("BeginApproach",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(board,null);
        static MechanicsCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("MechanicsCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");SessionState.SetBool("MechanicsCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>60)throw new Exception("Mechanics timeout");
                if(Time.frameCount<4)return;
                var context=GameContext.Instance;
                if(phase==0)
                {
                    board=UnityEngine.Object.FindAnyObjectByType<BoardController>();flow=UnityEngine.Object.FindAnyObjectByType<CurrentFlow>();
                    context.CameraManager.UnlockGroup(1);context.CameraManager.UnlockGroup(1);
                    var first=context.CameraManager.ActiveCamera;first.SetAccessible(false);
                    if(context.CameraManager.ActiveCamera==first)throw new Exception("Accessibility event fallback failed");first.SetAccessible(true);
                    flow.Begin();if(!flow.IsRunning)throw new Exception("Current did not begin");
                    Approach();context.PlayerMover.MoveTo(context.PlayerMover.transform.position);
                    phaseTime=Time.time;phase=1;
                }
                else if(phase==1 && Time.time-phaseTime>0.6f)
                {
                    if((bool)Field(board,"pending") || ((GameObject)Field(board,"fallenBoard")).activeSelf)throw new Exception("Board cancellation failed");
                    Approach();phase=2;
                }
                else if(phase==2 && ((GameObject)Field(board,"fallenBoard")).activeSelf && flow.IsFinished)
                {
                    if(((GameObject)Field(board,"standingBoard")).activeSelf || board.enabled)throw new Exception("Board idle state failed");
                    Debug.Log("MECHANICS_PLAY_PASSED: accessibility events, board arrival/cancel, current finish");Finish(0);
                }
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("MechanicsCheck",false);EditorApplication.Exit(code);}
    }
}
