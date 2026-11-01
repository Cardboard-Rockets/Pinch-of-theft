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
    public static class LockPowerDoorCheck
    {
        private static int phase,frame;
        private static CodeLock codeLock;
        private static Transform closed;
        private static T F<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void R(bool v,string m){if(!v)throw new Exception(m);}
        static LockPowerDoorCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("LockPowerDoorCheck",false))EditorApplication.update+=Tick;};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("LockPowerDoorCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            if(Time.frameCount<5||Time.frameCount<frame+3)return;
            try
            {
                var context=GameContext.Instance;
                if(phase==0)
                {
                    codeLock=Object.FindObjectsByType<CodeLock>().Single();var source=Object.FindObjectsByType<CodePuzzle>().Single();source.OnShieldActivated();source.ActivateTV();R(!codeLock.HasPower&&!codeLock.CanOpen&&!codeLock.GetComponent<InteractableSphere>().IsAvailable,"Lock powered by TV incorrectly");codeLock.Open();R(!Object.FindAnyObjectByType<KeypadUI>().IsOpen,"Lock opened without elec");
                    var camera=context.CameraManager.ActiveCamera.transform;var center=codeLock.GetComponent<Collider>().bounds.center;camera.position=center+Vector3.forward;camera.LookAt(center);Physics.SyncTransforms();frame=Time.frameCount;phase=1;
                }
                else if(phase==1)
                {
                    R(F<GameObject>(codeLock.GetComponent<NoPowerPrompt>(),"popup").activeSelf,"No energy popup absent");
                    Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("elec",StringComparison.OrdinalIgnoreCase)).Complete();R(codeLock.HasPower&&codeLock.CanOpen&&codeLock.GetComponent<InteractableSphere>().IsAvailable,"elec did not power lock");
                    var e=F<UnityEvent>(codeLock,"onSolved");var opener=(DoorOpener)e.GetPersistentTarget(0);closed=F<GameObject>(opener,"closedDoor").transform;var doorCenter=closed.GetComponent<Collider>().bounds.center;var fuse=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("fu2",StringComparison.OrdinalIgnoreCase));var center=fuse.GetComponent<Collider>().bounds.center;var direction=(center-doorCenter).normalized;var camera=context.CameraManager.ActiveCamera.transform;camera.position=doorCenter-direction*1.5f;camera.LookAt(center);Physics.SyncTransforms();frame=Time.frameCount;phase=2;
                }
                else if(phase==2)
                {
                    R(context.AimProbe.HasHit&&context.AimProbe.Collider.transform.IsChildOf(closed),"Closed door does not block ray");R(!Object.FindAnyObjectByType<SphereInteractor>().HasTarget,"fu2 targeted through door");Debug.Log("CLOSED_DOOR_BLOCKS_FU2_PASSED");
                    var source=Object.FindObjectsByType<CodePuzzle>().Single();codeLock.Open();R(codeLock.CheckCode(source.Code),"Lock correct code failed");Physics.SyncTransforms();frame=Time.frameCount;phase=3;
                }
                else if(phase==3)
                {
                    R(context.AimProbe.HasHit&&context.AimProbe.Collider.GetComponentInParent<InteractableSphere>()?.name.Equals("fu2",StringComparison.OrdinalIgnoreCase)==true,"fu2 not reachable after opening");
                    R(!F<GameObject>(codeLock.GetComponent<NoPowerPrompt>(),"popup").activeSelf,"No energy text remains");Debug.Log("LOCK_ELEC_POWER_AND_OPEN_DOOR_FU2_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("LockPowerDoorCheck",false);EditorApplication.Exit(code);}
    }
}
