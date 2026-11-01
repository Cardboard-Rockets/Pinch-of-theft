using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class FirstFloorCheck
    {
        private static int stage,frame;
        private static double began;
        private static Vector3 goal;
        private static PlayerMover mover;
        static FirstFloorCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("FirstFloorCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("FirstFloorCheck",true);EditorApplication.EnterPlaymode();}
        private static void R(bool v,string m){if(!v)throw new Exception(m);}
        private static void Tick()
        {
            if(Time.frameCount<10)return;
            try
            {
                if(EditorApplication.timeSinceStartup-began>35)throw new Exception("Door movement timeout");
                if(stage==0)
                {
                    var mesh=NavMesh.CalculateTriangulation();float max=mesh.vertices.Max(v=>v.y);R(max<2.9f,"Upper floor still walkable: "+max);Debug.Log("FIRST_FLOOR_NAV_ONLY_PASSED maxY="+max);
                    mover=GameContext.Instance.PlayerMover;var agent=mover.GetComponent<NavMeshAgent>();var guard=mover.GetComponent<PlayerCollisionGuard>();
                    var door=Object.FindObjectsByType<DoorOpener>().Single(d=>d.transform.parent.name.Equals("Door_code",StringComparison.OrdinalIgnoreCase));var bounds=door.GetComponent<Collider>().bounds;var normal=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;
                    bool Sample(Vector3 p,out Vector3 point){point=p;if(!Physics.Raycast(p,Vector3.down,out var hit,3f,~(1<<LayerMask.NameToLayer("Player")),QueryTriggerInteraction.Ignore))return false;if(!NavMesh.SamplePosition(hit.point,out var nav,0.25f,NavMesh.AllAreas))return false;point=nav.position;return true;}
                    R(Sample(bounds.center+normal*0.65f,out var start)&&Sample(bounds.center-normal*0.65f,out goal),"Door sample missing");R(!NavMesh.Raycast(start,goal,out _,NavMesh.AllAreas),"Closed leaf blocks NavMesh");R(Physics.GetIgnoreLayerCollision(mover.gameObject.layer,door.gameObject.layer),"Door collision pair not ignored");
                    R(door.GetComponent<Collider>().enabled && door.gameObject.activeInHierarchy && (GameContext.Instance.AimProbe.Mask & (1<<door.gameObject.layer))!=0,"Door ray collider unavailable enabled="+door.GetComponent<Collider>().enabled+" active="+door.gameObject.activeInHierarchy+" layer="+door.gameObject.layer+" mask="+GameContext.Instance.AimProbe.Mask);
                    guard.enabled=false;R(agent.Warp(start+Vector3.up*(agent.baseOffset*Mathf.Abs(mover.transform.lossyScale.y))),"Warp failed");Physics.SyncTransforms();guard.enabled=true;
                    R(mover.TryMoveToWithClearance(goal,0.3f,out goal),"Door destination rejected");stage=1;frame=Time.frameCount;
                }
                else if(Time.frameCount>frame+8&&!mover.IsMoving)
                {
                    var delta=mover.transform.position-goal;delta.y=0;R(delta.magnitude<0.25f,"Player stopped at closed door distance="+delta.magnitude);Debug.Log("CLOSED_FIRST_FLOOR_DOOR_PLAYER_PASS_PASSED");Debug.Log("DOOR_RAYCAST_COLLIDER_RETAINED_PASSED");
                    var box=Object.FindObjectsByType<DoorOpener>().Single(d=>d.transform.parent.name.Equals("Boxx",StringComparison.OrdinalIgnoreCase));
                    var collider=box.GetComponent<Collider>();var center=collider.bounds.center;
                    var fuse=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("fu2",StringComparison.OrdinalIgnoreCase));
                    var direction=(fuse.GetComponent<Collider>().bounds.center-center).normalized;var origin=center-direction*1.5f;
                    R(Physics.Raycast(origin,direction,out var hit,3f,GameContext.Instance.AimProbe.Mask,QueryTriggerInteraction.Ignore)&&hit.collider.transform.IsChildOf(box.transform),"Boxx no longer blocks fu2");
                    box.Open();Physics.SyncTransforms();
                    R(Physics.Raycast(origin,direction,out hit,3f,GameContext.Instance.AimProbe.Mask,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<InteractableSphere>()==fuse,"fu2 unavailable after Boxx opens");
                    Debug.Log("BOXX_FU2_CLOSED_OPEN_RAY_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("FirstFloorCheck",false);EditorApplication.Exit(code);}
    }
}



