using System.Linq;
using System.Reflection;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction.Editor
{
    public static class DoorSceneSetup
    {
        private static void Set(Object obj,string name,Object value){var s=new SerializedObject(obj);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var objects=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(t=>t.gameObject).ToArray();
            var closed=objects.Where(o=>o.name.Trim().ToLowerInvariant()=="door").ToArray();
            var open=objects.Where(o=>o.name.Trim().ToLowerInvariant()=="open_door").ToArray();
            if(closed.Length!=1 || open.Length!=1)throw new System.Exception("Door names ambiguous");
            var manager=Object.FindAnyObjectByType<CameraManager>();var mover=Object.FindAnyObjectByType<PlayerMover>();
            var controller=new GameObject("DoorController").AddComponent<DoorController>();
            Set(controller,"cameraManager",manager);Set(controller,"menu",Object.FindAnyObjectByType<CameraMenuUI>());Set(controller,"mover",mover);Set(controller,"marker",Object.FindAnyObjectByType<ClickMarker>(FindObjectsInactive.Include));
            Set(controller,"closedDoor",closed[0]);Set(controller,"openDoor",open[0]);Set(manager,"door",controller);
            var settings=new SerializedObject(controller);settings.FindProperty("mask").intValue=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));settings.ApplyModifiedPropertiesWithoutUndo();
            closed[0].SetActive(true);open[0].SetActive(false);
            var mesh=closed[0].GetComponent<MeshCollider>();if(mesh!=null)mesh.enabled=false;
            var collider=closed[0].GetComponent<BoxCollider>();if(collider==null)collider=closed[0].AddComponent<BoxCollider>();
            var filter=closed[0].GetComponent<MeshFilter>();
            if(filter!=null){var b=filter.sharedMesh.bounds;collider.center=b.center;collider.size=new Vector3(Mathf.Max(b.size.x,0.05f),Mathf.Max(b.size.y,0.05f),Mathf.Max(b.size.z,0.05f));}
            collider.enabled=true;collider.isTrigger=false;Physics.SyncTransforms();
            var bounds=collider.bounds;var point=new GameObject("DoorApproachPoint").transform;point.SetParent(closed[0].transform);Vector3 best=Vector3.zero;float distance=float.PositiveInfinity;
            if(!NavMesh.SamplePosition(mover.transform.position,out var start,3,NavMesh.AllAreas))throw new System.Exception("No player NavMesh");
            var path=new NavMeshPath();
            for(int i=0;i<64;i++)
            {
                float angle=i*Mathf.PI*2/64;var dir=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));var surface=collider.ClosestPoint(bounds.center+dir*10);var candidate=surface+dir*0.4f;candidate.y=start.position.y;
                if(!NavMesh.SamplePosition(candidate,out var hit,0.3f,NavMesh.AllAreas))continue;
                float gap=Mathf.Min(Vector3.Distance(hit.position+Vector3.up,collider.ClosestPoint(hit.position+Vector3.up)),Vector3.Distance(hit.position+Vector3.up*0.5f,collider.ClosestPoint(hit.position+Vector3.up*0.5f)));
                if(gap>0.5f || !NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)continue;
                float d=Vector3.Distance(start.position,hit.position);if(d>=distance)continue;distance=d;best=hit.position;
            }
            if(float.IsInfinity(distance))throw new System.Exception("No reachable door contact point");
            point.position=best;Set(controller,"approachPoint",point);
            var popup=new GameObject("DoorPopup",typeof(RectTransform),typeof(Canvas));popup.transform.SetParent(controller.transform);popup.transform.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);popup.transform.localScale=Vector3.one*0.005f;popup.GetComponent<RectTransform>().sizeDelta=new Vector2(260,70);popup.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var bg=popup.AddComponent<Image>();bg.color=new Color(0,0,0,0.8f);bg.raycastTarget=false;
            var label=new GameObject("Text",typeof(RectTransform),typeof(Text));label.transform.SetParent(popup.transform,false);label.GetComponent<RectTransform>().sizeDelta=new Vector2(260,70);var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=32;text.alignment=TextAnchor.MiddleCenter;text.text="Закрыто";text.raycastTarget=false;
            popup.SetActive(false);Set(controller,"popup",popup);
            var puzzle=Object.FindAnyObjectByType<CodePuzzle>();var onSolved=(UnityEvent)typeof(CodePuzzle).GetField("onSolved",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(puzzle);UnityEventTools.AddPersistentListener(onSolved,controller.Unlock);EditorUtility.SetDirty(puzzle);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("DOOR_SETUP_COMPLETE");
        }
    }
}
