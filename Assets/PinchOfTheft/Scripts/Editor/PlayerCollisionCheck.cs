using System;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class PlayerCollisionCheck
    {
        private static PlayerMover mover;
        private static NavMeshAgent agent;
        private static CapsuleCollider capsule;
        private static PlayerCollisionGuard guard;
        private static Collider obstacle;
        private static Vector3 goal;
        private static int phase;
        private static double began;
        private static float phaseTime;
        static PlayerCollisionCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CollisionCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");SessionState.SetBool("CollisionCheck",true);EditorApplication.EnterPlaymode();}
        private static void Place(Vector3 position){guard.enabled=false;agent.Warp(position);agent.ResetPath();Physics.SyncTransforms();guard.enabled=true;}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>40)throw new Exception("Collision test timeout phase="+phase+" player="+(mover!=null?mover.transform.position:Vector3.zero));
                if(Time.frameCount<4)return;
                if(phase==0)
                {
                    mover=GameContext.Instance.PlayerMover;agent=mover.GetComponent<NavMeshAgent>();capsule=mover.GetComponent<CapsuleCollider>();guard=mover.GetComponent<PlayerCollisionGuard>();
                    if(guard==null || !capsule.enabled || capsule.isTrigger)throw new Exception("Player collider/guard missing");
                    var fixture=new GameObject("Collision Test");fixture.transform.position=new Vector3(100,0,100);
                    var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.SetParent(fixture.transform,false);floor.transform.localScale=Vector3.one*2;
                    var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(fixture.transform,false);cube.transform.localPosition=Vector3.up;cube.transform.localScale=Vector3.one*2;obstacle=cube.GetComponent<Collider>();
                    Physics.SyncTransforms();var surface=fixture.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
                    Place(new Vector3(95,1.03f,100));goal=new Vector3(105,0,100);mover.MoveTo(goal);phase=1;
                }
                else if(phase==1)
                {
                    if(Physics.ComputePenetration(capsule,capsule.transform.position,capsule.transform.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth) && depth>0.03f)throw new Exception("Player penetrated baked cube");
                    var delta=mover.transform.position-goal;delta.y=0;if(delta.magnitude>0.3f)return;
                    Debug.Log("COLLISION_BYPASS_PASSED");
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Unbaked wall";wall.transform.position=new Vector3(100,1,105);wall.transform.localScale=new Vector3(4,2,0.4f);obstacle=wall.GetComponent<Collider>();Physics.SyncTransforms();
                    Place(new Vector3(100,1.03f,103));goal=new Vector3(100,0,108);mover.MoveTo(goal);phaseTime=Time.time;phase=2;
                }
                else if(phase==2 && Time.time-phaseTime>1f)
                {
                    if(mover.transform.position.z>obstacle.bounds.min.z-0.45f)throw new Exception("Player crossed unbaked wall");
                    if(mover.IsMoving)return;
                    Debug.Log("COLLISION_STOP_PASSED");
                    Place(new Vector3(100,1.03f,103));
                    if(!mover.TryMoveToWithClearance(new Vector3(100,0,105),1.5f,out goal))throw new Exception("No safe destination near door");
                    if(goal.z>obstacle.bounds.min.z-0.6f)throw new Exception("Destination has insufficient door clearance: "+goal);
                    GameContext.Instance.Marker.Show(goal);phaseTime=Time.time;phase=3;
                }
                else if(phase==3 && Time.time-phaseTime>0.5f && !mover.IsMoving)
                {
                    var delta=mover.transform.position-goal;delta.y=0;
                    if(delta.magnitude>0.25f)throw new Exception("Player stopped before safe destination: player="+mover.transform.position+" goal="+goal);
                    if(GameContext.Instance.Marker.gameObject.activeSelf)return;
                    Debug.Log("DESTINATION_CLEARANCE_PASSED");
                    if(!mover.TryMoveToWithClearance(new Vector3(98,0,102),1.5f,out goal))throw new Exception("Movement after door click failed");
                    phaseTime=Time.time;phase=4;
                }
                else if(phase==4 && Time.time-phaseTime>0.5f && !mover.IsMoving)
                {
                    var delta=mover.transform.position-goal;delta.y=0;
                    if(delta.magnitude>0.25f)throw new Exception("Player stuck after door click");
                    Debug.Log("MOVEMENT_AFTER_CLEARANCE_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("CollisionCheck",false);EditorApplication.Exit(code);}
    }
}
