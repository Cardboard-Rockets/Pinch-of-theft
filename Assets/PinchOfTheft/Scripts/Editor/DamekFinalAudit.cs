using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using PinchOfTheft.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class DamekFinalAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            foreach(var p in Object.FindObjectsByType<PlayerMover>(FindObjectsInactive.Include,FindObjectsSortMode.None))Debug.Log("PLAYER_OBJECT "+HouseCircuitAudit.Path(p.transform));
            foreach(var d in Object.FindObjectsByType<DoorOpener>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var s=new SerializedObject(d);var closed=(GameObject)s.FindProperty("closedDoor").objectReferenceValue;var b=closed.GetComponent<Renderer>().bounds;var o=closed.GetComponent<NavMeshObstacle>();
                Debug.Log("DAMEK_DOOR "+HouseCircuitAudit.Path(closed.transform)+" worldSize="+b.size+" carving="+(o!=null&&o.carving)+" ignore="+closed.GetComponent<NavMeshModifier>().ignoreFromBuild);
            }
            var mover=Object.FindObjectsByType<PlayerMover>().Single(p=>p.name=="Damek");var a=mover.GetComponent<NavMeshAgent>();Debug.Log("DAMEK_AGENT speed="+a.speed+" angular="+a.angularSpeed+" accel="+a.acceleration+" stop="+a.stoppingDistance+" priority="+a.avoidancePriority+" braking="+a.autoBraking+" avoidance="+a.obstacleAvoidanceType);
            Debug.Log("DAMEK_MARKER size="+mover.GetComponentInChildren<ClickMarker>(true).GetComponent<Renderer>().bounds.size);
            PrefabSetup.Validate();Debug.Log("DAMEK_FINAL_VALIDATION_PASSED");
        }
    }
}
