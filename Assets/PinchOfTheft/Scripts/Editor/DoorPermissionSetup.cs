using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using PinchOfTheft.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class DoorPermissionSetup
    {
        private static void Set(Object o,string n,object v){var s=new SerializedObject(o);var p=s.FindProperty(n);if(v is bool b)p.boolValue=b;else if(v is float f)p.floatValue=f;else p.objectReferenceValue=v as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        private static UnityEvent Event(Object o,string n)=>(UnityEvent)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void Wire(Object o,string field,UnityAction action){var e=Event(o,field);for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==action.Target as Object&&e.GetPersistentMethodName(i)==action.Method.Name)return;UnityEventTools.AddPersistentListener(e,action);EditorUtility.SetDirty(o);}
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var ctx=Object.FindAnyObjectByType<GameContext>();var puzzle=Object.FindObjectsByType<CodePuzzle>().Single();
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var snapshots=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            foreach(var closed in all.Where(t=>new[]{"door","movable"}.Contains(t.name.Trim().ToLowerInvariant())&&t.GetComponent<Renderer>()!=null&&t.GetComponent<Renderer>().bounds.center.y<3.8f))
            {
                var parent=closed.parent;var bounds=closed.GetComponent<Renderer>().bounds;
                var opened=parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t!=closed&&t.name.Trim().Equals("open_door",StringComparison.OrdinalIgnoreCase));
                if(opened==null)
                {
                    var go=new GameObject("open_door",typeof(MeshFilter),typeof(MeshRenderer));opened=go.transform;opened.SetParent(parent,false);opened.localPosition=closed.localPosition;opened.localRotation=closed.localRotation;opened.localScale=closed.localScale;go.GetComponent<MeshFilter>().sharedMesh=closed.GetComponent<MeshFilter>().sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterials=closed.GetComponent<Renderer>().sharedMaterials;
                    Vector3 pivot=bounds.center;int width=bounds.size.x>=bounds.size.z?0:2;pivot[width]=Mathf.Abs(bounds.min[width]-parent.position[width])<Mathf.Abs(bounds.max[width]-parent.position[width])?bounds.min[width]:bounds.max[width];opened.RotateAround(pivot,Vector3.up,90f);
                }
                closed.gameObject.SetActive(true);opened.gameObject.SetActive(false);
                if(closed.GetComponentsInChildren<Collider>(true).Length==0)closed.gameObject.AddComponent<BoxCollider>();
                foreach(var collider in closed.GetComponentsInChildren<Collider>(true))collider.gameObject.layer=0;
                var modifier=closed.GetComponent<NavMeshModifier>();if(modifier==null)modifier=closed.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
                var obstacle=closed.GetComponent<NavMeshObstacle>();if(obstacle==null)obstacle=closed.gameObject.AddComponent<NavMeshObstacle>();var mesh=closed.GetComponent<MeshFilter>().sharedMesh;obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=mesh.bounds.center;obstacle.size=mesh.bounds.size;obstacle.carving=true;obstacle.enabled=true;obstacle.carveOnlyStationary=false;
                var opener=closed.GetComponent<DoorOpener>();if(opener==null)opener=closed.gameObject.AddComponent<DoorOpener>();Set(opener,"closedDoor",closed.gameObject);Set(opener,"openedDoor",opened.gameObject);
                var service=parent.Find("DoorInteraction");if(service==null){service=new GameObject("DoorInteraction").transform;service.SetParent(parent,false);}
                var controller=service.GetComponent<DoorController>();if(controller==null)controller=service.gameObject.AddComponent<DoorController>();
                Set(controller,"cameraManager",ctx.CameraManager);Set(controller,"menu",ctx.Menu);Set(controller,"mover",Object.FindObjectsByType<PlayerMover>().Single(p=>p.name=="Damek"));Set(controller,"closedDoor",closed.gameObject);Set(controller,"openDoor",opened.gameObject);Set(controller,"opener",opener);Set(controller,"chooseReachableSide",true);Set(controller,"contactTolerance",0.25f);
                var approach=service.Find("ApproachPoint");if(approach==null){approach=new GameObject("ApproachPoint").transform;approach.SetParent(service,false);approach.position=bounds.center;}
                float floor=1.15f;var normal=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;if(Physics.Raycast(bounds.center+normal*0.5f,Vector3.down,out var hit,3f,~(1<<LayerMask.NameToLayer("Player")),QueryTriggerInteraction.Ignore))floor=hit.point.y;approach.position=new Vector3(approach.position.x,floor,approach.position.z);Set(controller,"approachPoint",approach);
                var popup=service.Find("Popup");if(popup==null){var source=Object.FindObjectsByType<CurrentFlow>().First(f=>f.name=="wire1");var label=(GameObject)new SerializedObject(source).FindProperty("noPowerLabel").objectReferenceValue;popup=Object.Instantiate(label,service).transform;popup.name="Popup";popup.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);popup.rotation=Quaternion.identity;var scale=service.lossyScale;popup.localScale=new Vector3(0.005f/scale.x,0.005f/scale.y,0.005f/scale.z);}
                popup.gameObject.SetActive(false);Set(controller,"popup",popup.gameObject);
                bool codeDoor=parent.name.Equals("Door_code",StringComparison.OrdinalIgnoreCase);
                if(codeDoor){var e=Event(puzzle,"onSolved");for(int i=e.GetPersistentEventCount()-1;i>=0;i--)if(e.GetPersistentTarget(i)==opener&&e.GetPersistentMethodName(i)=="Open")UnityEventTools.RemovePersistentListener(e,i);Wire(puzzle,"onSolved",controller.Unlock);}
                else Wire(puzzle,"onCodeGenerated",controller.Unlock);
                Debug.Log("DOOR_PERMISSION "+HouseCircuitAudit.Path(closed)+" unlock="+(codeDoor?"correct code":"TV power unlock"));
            }
            var surface=Object.FindObjectsByType<NavMeshSurface>().Single();Physics.SyncTransforms();surface.BuildNavMesh();var built=surface.navMeshData;var data=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Scenes/HouseNavMesh.asset");surface.RemoveData();EditorUtility.CopySerialized(built,data);surface.navMeshData=data;surface.AddData();Object.DestroyImmediate(built);EditorUtility.SetDirty(data);
            foreach(var x in snapshots){var t=x.Key;var v=x.Value;if(t.localPosition!=v.Item1||t.localRotation!=v.Item2||t.localScale!=v.Item3)throw new Exception("Moved existing model "+t.name);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();PrefabSetup.Validate();Debug.Log("DOOR_PERMISSIONS_SETUP_PASSED");
        }
    }
}
