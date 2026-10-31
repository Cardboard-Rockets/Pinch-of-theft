using System;
using System.Reflection;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PinchOfTheft.Interaction.Editor
{
    [InitializeOnLoad]
    public static class BoardPlayCheck
    {
        private static BoardController board;
        private static PlayerMover mover;
        private static GameObject standing;
        private static GameObject fallen;
        private static double start;
        private static int stage;
        private static object Field(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        static BoardPlayCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("BoardCheck",false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");SessionState.SetBool("BoardCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-start>35)throw new Exception("Board movement timeout player="+mover.transform.position+" dest="+mover.Destination+" pending="+Field(board,"pending")+" moving="+mover.IsMoving+" contact="+typeof(BoardController).GetMethod("IsTouchingBoard",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(board,null));
                if(Time.frameCount<3)return;
                if(stage==0)
                {
                    board=UnityEngine.Object.FindAnyObjectByType<BoardController>();mover=UnityEngine.Object.FindAnyObjectByType<PlayerMover>();standing=(GameObject)Field(board,"standingBoard");fallen=(GameObject)Field(board,"fallenBoard");
                    var manager=UnityEngine.Object.FindAnyObjectByType<CameraManager>();var camera=manager.ActiveCamera.transform;var bounds=standing.GetComponent<Collider>().bounds;
                    camera.position=bounds.center+standing.transform.forward*3;camera.LookAt(bounds.center);Physics.SyncTransforms();
                    typeof(BoardController).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(board,null);
                    if(!board.IsAimed || !((GameObject)Field(board,"popup")).activeSelf)throw new Exception("Board aim/popup failed");
                    typeof(BoardController).GetMethod("BeginApproach",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(board,null);
                    if(!standing.activeSelf || fallen.activeSelf)throw new Exception("Board fell before arrival");stage=1;
                }
                else if(stage==1 && fallen.activeSelf)
                {
                    bool contact=(bool)typeof(BoardController).GetMethod("IsTouchingBoard",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(board,null);
                    // Неактивная доска сохраняет геометрию для проверки конечного расстояния.
                    if(standing.activeSelf || board.IsAimed)throw new Exception("Board final state failed");
                    Debug.Log("BOARD_PLAY_PASSED player="+mover.transform.position);
                    Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("BoardCheck",false);EditorApplication.Exit(code);}
    }
}

