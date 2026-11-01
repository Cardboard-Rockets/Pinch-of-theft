using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseWireRepairCheck
    {
        private static int phase,frame;private static double started;
        private static BoardController repair;private static InteractableSphere unlock;
        private static Keyboard keyboard;
        private static T F<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void R(bool v,string n){if(!v)throw new Exception(n);}
        static HouseWireRepairCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("WireRepairCheck",false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("WireRepairCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            if(Time.frameCount<8||Time.frameCount<frame+4)return;
            try
            {
                if(EditorApplication.timeSinceStartup-started>35)throw new Exception("Repair timeout phase="+phase+" player="+GameContext.Instance.PlayerMover.transform.position);
                if(phase==0)
                {
                    repair=Object.FindObjectsByType<BoardController>().Single(c=>c.name=="WireRepair");
                    unlock=Object.FindObjectsByType<InteractableSphere>(FindObjectsInactive.Include).Single(c=>c.name=="Unlock cam21");
                    R(!F<GameObject>(repair,"activeEffect").activeSelf&&!unlock.IsAvailable,"Initial repair state");
                    var onlocker=Object.FindObjectsByType<InteractableSphere>(FindObjectsInactive.Include).Single(c=>c.name=="onlock onlocker");
                    onlocker.SetAvailable(true);onlocker.Complete();
                    R(F<GameObject>(repair,"activeEffect").activeSelf&&!unlock.IsAvailable,"Power bypassed repair");
                    foreach(var d in Object.FindObjectsByType<DoorOpener>())d.Open();
                    Object.FindObjectsByType<BoardController>().Single(c=>c.name=="BoxController").gameObject.SetActive(false);
                    var box=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Box_A1");box.gameObject.SetActive(false);
                    var manager=GameContext.Instance.CameraManager;manager.UnlockGroup(1);manager.SelectCamera(manager.Cameras.Single(c=>c.Group==1&&c.Slot==4));
                    var bounds=F<GameObject>(repair,"standingBoard").GetComponent<Collider>().bounds;
                    manager.ActiveCamera.transform.LookAt(bounds.center);
                    InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();
                    Physics.SyncTransforms();phase=1;frame=Time.frameCount+20;return;
                }
                if(phase==1)
                {
                    R(repair.IsAimed,"Camera14 repair not aimed, first hit="+GameContext.Instance.AimProbe.Collider);
                    R(F<GameObject>(repair,"popup").activeSelf&&F<GameObject>(repair,"popup").GetComponentInChildren<Text>().text.Contains("Починить"),"Repair caption");
                    var path=new NavMeshPath();NavMesh.CalculatePath(GameContext.Instance.PlayerMover.transform.position,F<Transform>(repair,"approachPoint").position,NavMesh.AllAreas,path);R(path.status==NavMeshPathStatus.PathComplete,"Repair approach unreachable");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F));phase=2;frame=Time.frameCount;return;
                }
                if(phase==2)
                {
                    if(!F<GameObject>(repair,"fallenBoard").activeSelf){R(!unlock.IsAvailable,"Early unlock");return;}
                    R(!F<GameObject>(repair,"activeEffect").activeSelf&&unlock.IsAvailable&&!repair.enabled,"Repair completion state");
                    Debug.Log("WIRE_REPAIR_CAMERA14_E_APPROACH_AND_UNLOCK_PASSED");Finish(0);
                }
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("WireRepairCheck",false);EditorApplication.Exit(code);}
    }
}
