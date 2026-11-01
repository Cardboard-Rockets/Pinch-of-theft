using System;
using System.Linq;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class DamekDoorCheck
    {
        private static int stage,frame;
        private static Vector3 a,b;
        private static DoorOpener door;
        static DamekDoorCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DamekDoor",false))EditorApplication.update+=Tick;};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("DamekDoor",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            if(Time.frameCount<10||Time.frameCount<frame+10)return;
            try
            {
                if(stage==0)
                {
                    door=Object.FindObjectsByType<DoorOpener>().Single(d=>d.transform.parent.name.Equals("Door_code",StringComparison.OrdinalIgnoreCase));var bounds=door.GetComponent<Collider>().bounds;var normal=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;
                    bool Sample(Vector3 p,out Vector3 point){point=p;if(!Physics.Raycast(p,Vector3.down,out var hit,3,~(1<<LayerMask.NameToLayer("Player")),QueryTriggerInteraction.Ignore))return false;if(!NavMesh.SamplePosition(hit.point,out var nav,0.25f,NavMesh.AllAreas))return false;point=nav.position;return true;}
                    if(!Sample(bounds.center+normal*0.65f,out a)||!Sample(bounds.center-normal*0.65f,out b))throw new Exception("Cannot sample both sides of code door");
                    if(!NavMesh.Raycast(a,b,out _,NavMesh.AllAreas))throw new Exception("Closed door carve not blocking");Debug.Log("DAMEK_CLOSED_DOOR_CARVE_PASSED a="+a+" b="+b);door.Open();frame=Time.frameCount;stage=1;
                }
                else
                {
                    if(NavMesh.Raycast(a,b,out _,NavMesh.AllAreas))throw new Exception("Open door still blocks NavMesh");var path=new NavMeshPath();if(!NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("No path through open door");Debug.Log("DAMEK_OPEN_DOOR_NAV_PATH_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("DamekDoor",false);EditorApplication.Exit(code);}
    }
}
