using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
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
    public static class DamekCheck
    {
        private static int phase;
        private static double began;
        private static PlayerMover mover;
        private static Vector3 inside,road,goal;
        private static int stoppedFrame=-1;
        private static void R(bool v,string m){if(!v)throw new Exception(m);}
        static DamekCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DamekCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetVector3("DamekOriginal",Object.FindObjectsByType<PlayerMover>().Single(p=>p.name=="Damek").transform.position);SessionState.SetBool("DamekCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Damek timeout phase="+phase+" position="+(mover!=null?mover.transform.position:Vector3.zero));
                if(Time.frameCount<5)return;
                var context=GameContext.Instance;
                if(phase==0)
                {
                    mover=context.PlayerMover;R(mover!=null&&mover.name=="Damek"&&context.Marker!=null,"Registration failed");var agent=mover.GetComponent<NavMeshAgent>();R(agent.isOnNavMesh,"Damek off NavMesh");R(Vector3.Distance(mover.transform.position,SessionState.GetVector3("DamekOriginal",Vector3.zero))<0.01f,"Damek moved at start "+mover.transform.position);
                    R(NavMesh.SamplePosition(mover.transform.position-Vector3.up*(agent.baseOffset*Mathf.Abs(mover.transform.lossyScale.y)),out var initialHit,1,NavMesh.AllAreas),"No initial nav sample");inside=initialHit.position;
                    var roadObject=Object.FindObjectsByType<Transform>().Single(t=>t.name.Equals("houseroad3",StringComparison.OrdinalIgnoreCase));var bounds=roadObject.GetComponent<Renderer>().bounds;
                    R(NavMesh.SamplePosition(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z),out var hit,2,NavMesh.AllAreas),"No road NavMesh");road=hit.position;
                    var path=new NavMeshPath();bool connected=NavMesh.CalculatePath(inside,road,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;Debug.Log("DAMEK_CONNECTIVITY connected="+connected+" inside="+inside+" road="+road+" corners="+string.Join(";",path.corners.Select(v=>v.ToString())));
                    foreach(var camera in Object.FindObjectsByType<CCTVCamera>().Where(c=>c.Group==0)){var center=mover.GetComponent<CapsuleCollider>().bounds.center;var p=camera.View.WorldToViewportPoint(center);bool hitTarget=Physics.Raycast(camera.transform.position,(center-camera.transform.position).normalized,out var rayHit,Vector3.Distance(center,camera.transform.position)+0.1f,~(1<<2),QueryTriggerInteraction.Ignore)&&rayHit.collider.transform.IsChildOf(mover.transform);Debug.Log("DAMEK_VISIBLE CAM"+camera.CameraName+" inFrustum="+(p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1)+" clear="+hitTarget);}
                    if(!connected){Debug.Log("DAMEK_ROAD_CENTER_BLOCKED");float best=float.PositiveInfinity;Vector3 original=road;for(int x=0;x<=10;x++)for(int z=0;z<=10;z++){var sample=new Vector3(Mathf.Lerp(bounds.min.x+0.4f,bounds.max.x-0.4f,x/10f),bounds.max.y,Mathf.Lerp(bounds.min.z+0.4f,bounds.max.z-0.4f,z/10f));if(!NavMesh.SamplePosition(sample,out var near,0.4f,NavMesh.AllAreas))continue;var test=new NavMeshPath();if(!NavMesh.CalculatePath(inside,near.position,NavMesh.AllAreas,test)||test.status!=NavMeshPathStatus.PathComplete)continue;float score=(near.position-original).sqrMagnitude;if(score>=best)continue;best=score;road=near.position;connected=true;}Debug.Log("DAMEK_ENTRANCE_ROAD connected="+connected+" point="+road);}R(connected,"Road and house disconnected");R(mover.TryMoveToWithClearance(road,1.5f,out goal),"Road destination rejected");context.Marker.Show(goal);phase=1;Debug.Log("DAMEK_START_PASSED");
                }
                else if(phase==1||phase==2)
                {
                    if(mover.IsMoving){stoppedFrame=-1;return;}
                    if(stoppedFrame<0){stoppedFrame=Time.frameCount;return;}if(Time.frameCount<=stoppedFrame+2)return;
                    var delta=mover.transform.position-goal;delta.y=0;R(delta.magnitude<0.3f,"Agent stopped before goal phase="+phase+" distance="+delta.magnitude+" position="+mover.transform.position+" goal="+goal);R(!context.Marker.gameObject.activeSelf,"Marker remained");
                    if(phase==1){Debug.Log("DAMEK_ROAD_ARRIVAL_PASSED");R(mover.TryMoveToWithClearance(inside,1.5f,out goal),"Inside destination rejected");context.Marker.Show(goal);phase=2;stoppedFrame=-1;}
                    else{R(Object.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1,"Listeners");Debug.Log("DAMEK_HOUSE_RETURN_PASSED");Finish(0);}
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("DamekCheck",false);EditorApplication.Exit(code);}
    }
}



