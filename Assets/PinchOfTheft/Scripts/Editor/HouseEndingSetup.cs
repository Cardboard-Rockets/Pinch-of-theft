using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using System;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Interaction;
using UnityEditor.Events;
using UnityEngine.Events;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class HouseEndingSetup
    {
        private static T Add<T>(GameObject o) where T:Component { var c=o.GetComponent<T>();if(c==null)c=o.AddComponent<T>();return c; }
        private static void Set(Object o,string name,object value)
        {var s=new SerializedObject(o);var p=s.FindProperty(name);if(value is bool b)p.boolValue=b;else if(value is float f)p.floatValue=f;else if(value is int i)p.intValue=i;else if(value is string text)p.stringValue=text;else p.objectReferenceValue=value as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void Wire(Object o,string field,UnityAction callback)
        {
            var e=(UnityEvent)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
            for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==callback.Target as Object&&e.GetPersistentMethodName(i)==callback.Method.Name)return;
            UnityEventTools.AddPersistentListener(e,callback);EditorUtility.SetDirty(o);
        }
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var saved=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            Transform Find(string n)=>all.Single(t=>t.name.Equals(n,StringComparison.OrdinalIgnoreCase));
            var context=Object.FindAnyObjectByType<GameContext>();if(context==null)throw new Exception("Core отсутствует");
            var door=Find("Door_Laptop");var closed=door.Find("Movable");var opened=door.Find("Movable (1)");
            if(closed==null||opened==null||opened.gameObject.activeSelf)throw new Exception("Неоднозначные состояния Door_Laptop");
            var first=all.Single(t=>Path(t)=="Interact/Laptop");
            var last=all.Single(t=>Path(t)=="House/interactable/Laptop (1)");
            var entry=Find("Sek_vei");var destination=Find("Box1 (19)");var secret=Find("Sek_door");
            var root=all.FirstOrDefault(t=>t.name=="EndingSequence");if(root==null)root=new GameObject("EndingSequence").transform;
            var sequence=Add<EndingSequence>(root.gameObject);var opener=Add<DoorOpener>(root.gameObject);
            Set(opener,"closedDoor",closed.gameObject);Set(opener,"openedDoor",opened.gameObject);
            var flows=Object.FindObjectsByType<CurrentFlow>();var flow1=flows.Single(f=>f.name=="wire1");var flow2=flows.Single(f=>f.name=="wire2");
            Set(sequence,"firstCircuit",flow1);Set(sequence,"secondCircuit",flow2);Set(sequence,"laptopDoor",opener);
            Wire(flow1,"onFinished",sequence.CheckCircuits);Wire(flow2,"onFinished",sequence.CheckCircuits);
            var laptop=Add<InteractableSphere>(first.gameObject);
            Set(laptop,"unlockGroup",-1);Set(laptop,"singleUse",true);Set(laptop,"startAvailable",false);Set(laptop,"holdDuration",.01f);Set(laptop,"preventReactivation",true);Set(laptop,"cameraManager",context.CameraManager);
            first.gameObject.layer=LayerMask.NameToLayer("Interactable");
            var prompt=first.GetComponentInChildren<KeyPrompt>(true);
            if(prompt==null)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab"),first);
                go.name="KeyPrompt";go.transform.position=first.GetComponentInChildren<Renderer>().bounds.center+Vector3.up*.3f;
                var scale=first.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);prompt=go.GetComponentInChildren<KeyPrompt>(true);
            }
            Set(laptop,"prompt",prompt);Set(sequence,"firstLaptop",laptop);Wire(laptop,"onCompleted",sequence.UnlockPassage);
            Set(sequence,"secretDoor",secret.gameObject);Set(sequence,"destination",destination);
            Set(sequence,"roomCamera",Object.FindObjectsByType<CCTVCamera>().Single(c=>c.Group==3&&c.Slot==1));
            if(!NavMesh.SamplePosition(destination.position,out var spawn,1f,NavMesh.AllAreas))throw new Exception("Нет NavMesh у Box1 (19)");
            var enterController=Approach(root,"SecretEntry",entry,"Заехать",context,Find("Damek").position);
            Set(sequence,"entry",enterController);Wire(enterController,"onFallen",sequence.EnterRoom);
            var finalController=Approach(root,"FinalLaptop",last,"Использовать",context,spawn.position);
            Set(sequence,"finalLaptop",finalController);Wire(finalController,"onFallen",sequence.Finish);
            foreach(var p in saved)if(p.Key.localPosition!=p.Value.Item1||p.Key.localRotation!=p.Value.Item2||p.Key.localScale!=p.Value.Item3)throw new Exception("Изменён Transform "+Path(p.Key));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();PrefabSetup.Validate();Debug.Log("ENDING_SETUP_PASSED");
        }
        private static BoardController Approach(Transform root,string name,Transform target,string action,GameContext context,Vector3 source)
        {
            var service=root.Find(name);if(service==null){service=new GameObject(name).transform;service.SetParent(root,false);}
            var board=Add<BoardController>(service.gameObject);
            Set(board,"cameraManager",context.CameraManager);Set(board,"menu",context.Menu);Set(board,"mover",Object.FindObjectsByType<PinchOfTheft.Movement.PlayerMover>().Single(p=>p.name=="Damek"));
            Set(board,"standingBoard",target.gameObject);Set(board,"fallenBoard",target.gameObject);Set(board,"hideStandingOnComplete",false);Set(board,"startAvailable",false);Set(board,"requireContact",false);Set(board,"popupText",action);Set(board,"arriveTolerance",.25f);
            var bounds=target.GetComponentsInChildren<Renderer>(true)[0].bounds;
            var point=service.Find("ApproachPoint");
            if(point==null)
            {
                float best=float.PositiveInfinity;Vector3 chosen=default;var path=new NavMeshPath();
                if(!NavMesh.SamplePosition(source,out var start,2f,NavMesh.AllAreas))throw new Exception("Нет NavMesh у начала "+name);
                for(int ring=0;ring<4;ring++)for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8;var candidate=new Vector3(bounds.center.x,start.position.y,bounds.center.z)+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(.6f+ring*.3f);
                    if(!NavMesh.SamplePosition(candidate,out var hit,.7f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-start.position.y)>.6f)continue;
                    NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path);
                    if(name=="FinalLaptop"&&path.status!=NavMeshPathStatus.PathComplete)continue;
                    float score=Vector3.Distance(hit.position,new Vector3(bounds.center.x,hit.position.y,bounds.center.z))+.01f*Vector3.Distance(source,hit.position);
                    if(score>=best)continue;best=score;chosen=hit.position;
                }
                if(float.IsInfinity(best))throw new Exception("Нет достижимой точки "+name);
                point=new GameObject("ApproachPoint").transform;point.SetParent(service,false);point.position=chosen;
            }
            Set(board,"approachPoint",point);
            var popup=service.Find("Popup");if(popup==null)popup=(Transform)typeof(HousePropSetup).GetMethod("CreatePopup",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{service,bounds,action});
            popup.gameObject.SetActive(false);Set(board,"popup",popup.gameObject);
            Debug.Log("ENDING_APPROACH "+name+" "+point.position.ToString("F3"));return board;
        }
        public static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
        public static void Audit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var t in all.Where(t=>new[]{"door_laptop","laptop","laptop (1)","sek_door","sek_vei","box1 (19)"}.Contains(t.name.ToLowerInvariant())))
            {
                Debug.Log("ENDING_OBJECT "+Path(t)+" pos="+t.position.ToString("F3")+" active="+t.gameObject.activeSelf);
                foreach(var r in t.GetComponentsInChildren<Renderer>(true))Debug.Log("ENDING_RENDERER "+Path(r.transform)+" bounds="+r.bounds+" active="+r.gameObject.activeSelf);
                foreach(var c in t.GetComponentsInChildren<Collider>(true))Debug.Log("ENDING_COLLIDER "+Path(c.transform)+" "+c.GetType().Name);
                if(NavMesh.SamplePosition(t.position,out var sample,3f,NavMesh.AllAreas))Debug.Log("ENDING_NAV "+Path(t)+" sample="+sample.position.ToString("F3")+" offset="+Vector3.Distance(sample.position,t.position));
            }
        }
    }
}
