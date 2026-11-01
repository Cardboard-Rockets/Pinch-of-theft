using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class HouseCircuitSetup
    {
        private static void Set(Object obj,string name,object value){var s=new SerializedObject(obj);var p=s.FindProperty(name);if(value is bool b)p.boolValue=b;else if(value is float f)p.floatValue=f;else if(value is int i)p.intValue=i;else p.objectReferenceValue=value as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        private static UnityEvent Event(Object obj,string name)=>(UnityEvent)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        private static InteractableSphere Interaction(Transform target,CameraManager manager,float duration,bool available)
        {
            if(target.GetComponentsInChildren<Collider>(true).Length==0)target.gameObject.AddComponent<BoxCollider>();
            if(target.gameObject.layer==0)target.gameObject.layer=LayerMask.NameToLayer("Interactable");
            var interaction=target.GetComponent<InteractableSphere>();bool created=interaction==null;
            if(created)interaction=target.gameObject.AddComponent<InteractableSphere>();
            var prompt=target.GetComponentInChildren<KeyPrompt>(true);
            if(prompt==null)
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab"),target);
                var scale=target.lossyScale;root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);root.transform.rotation=Quaternion.identity;
                var bounds=target.GetComponentsInChildren<Collider>(true)[0].bounds;root.transform.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);prompt=root.GetComponentInChildren<KeyPrompt>(true);
            }
            Set(interaction,"cameraManager",manager);Set(interaction,"prompt",prompt);
            if(created){Set(interaction,"unlockGroup",-1);Set(interaction,"singleUse",true);Set(interaction,"startAvailable",available);Set(interaction,"holdDuration",duration);}
            return interaction;
        }
        private static void Wire(InteractableSphere target,CurrentFlow flow,string method)
        {
            var e=Event(target,"onCompleted");for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==flow&&e.GetPersistentMethodName(i)==method)return;
            if(method=="SetPowered")UnityEventTools.AddBoolPersistentListener(e,flow.SetPowered,true);
            else if(method=="ActivateFuse")UnityEventTools.AddPersistentListener(e,flow.ActivateFuse);
            else UnityEventTools.AddPersistentListener(e,flow.Begin);
            EditorUtility.SetDirty(target);
        }
        public static void Run()
        {
            // Старые сцены и шаблон остаются цепями без обязательного питания.
            foreach(var path in new[]{"Assets/Scenes/test_Scene.unity","Assets/Scenes/Sandbox_Empty.unity"})
            {
                var old=EditorSceneManager.OpenScene(path);bool dirty=false;
                foreach(var f in Object.FindObjectsByType<CurrentFlow>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                {
                    var so=new SerializedObject(f);if(!so.FindProperty("requirePower").boolValue)continue;
                    so.FindProperty("requirePower").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();dirty=true;
                }
                if(dirty){EditorSceneManager.MarkSceneDirty(old);EditorSceneManager.SaveScene(old);}
            }
            const string prefabPath="Assets/PinchOfTheft/Prefabs/Puzzle/CurrentCircuit.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(prefabPath);foreach(var f in prefab.GetComponentsInChildren<CurrentFlow>(true))Set(f,"requirePower",false);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);PrefabUtility.UnloadPrefabContents(prefab);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            if(Object.FindAnyObjectByType<GameContext>()==null)throw new Exception("Core missing");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var snapshots=all.ToDictionary(t=>t,t=>(t.name,t.parent,t.localPosition,t.localRotation,t.localScale));
            Transform Unique(string name){var a=all.Where(t=>HouseCircuitAudit.Normalize(t.name)==name).ToArray();if(a.Length!=1)throw new Exception(name+" candidates: "+string.Join(",",a.Select(HouseCircuitAudit.Path)));return a[0];}
            var start=Unique("start");var finish=Unique("finish");var elec=Unique("elec");var fuse=Unique("fu1");var current=Unique("sparkle1");var root=Unique("wire1");
            var flow=root.GetComponent<CurrentFlow>();bool created=flow==null;if(created)flow=root.gameObject.AddComponent<CurrentFlow>();
            Set(flow,"startPoint",start);Set(flow,"finishPoint",finish);Set(flow,"currentObject",current);Set(flow,"fuse",fuse);Set(flow,"wireRoot",root);
            if(created){Set(flow,"requirePower",true);Set(flow,"powered",false);Set(flow,"fuseActivated",false);Set(flow,"hideUntilStart",true);}
            var manager=Object.FindAnyObjectByType<CameraManager>();
            var startInteraction=Interaction(start,manager,0.3f,false);var energy=Interaction(elec,manager,2f,true);var fuseInteraction=Interaction(fuse,manager,1f,true);
            Set(flow,"startInteractable",startInteraction);Wire(startInteraction,flow,"Begin");Wire(energy,flow,"SetPowered");Wire(fuseInteraction,flow,"ActivateFuse");
            var popup=start.Find("CircuitNoPower");
            if(popup==null)
            {
                var go=new GameObject("CircuitNoPower",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Billboard));popup=go.transform;popup.SetParent(start,false);
                var scale=start.lossyScale;popup.localScale=new Vector3(0.005f/scale.x,0.005f/scale.y,0.005f/scale.z);popup.rotation=Quaternion.identity;
                var bounds=start.GetComponent<Collider>().bounds;popup.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);go.GetComponent<RectTransform>().sizeDelta=new Vector2(240,70);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Image>().color=new Color(0.04f,0.04f,0.04f,0.95f);go.GetComponent<Image>().raycastTarget=false;
                var text=new GameObject("Text",typeof(RectTransform),typeof(Text));text.transform.SetParent(popup,false);var label=text.GetComponent<Text>();label.rectTransform.sizeDelta=new Vector2(230,60);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=30;label.color=Color.white;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.text="Нет энергии";
            }
            Set(flow,"noPowerLabel",popup.gameObject);
            foreach(var collider in current.GetComponentsInChildren<Collider>(true)){collider.enabled=false;EditorUtility.SetDirty(collider);}
            foreach(var body in current.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;EditorUtility.SetDirty(body);}
            current.gameObject.SetActive(false);
            Physics.SyncTransforms();flow.ValidatePath();
            foreach(var pair in snapshots){var t=pair.Key;var s=pair.Value;if(t==null||t.name!=s.Item1||t.parent!=s.Item2||t.localPosition!=s.Item3||t.localRotation!=s.Item4||t.localScale!=s.Item5)throw new Exception("Transform changed "+s.Item1);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);PrefabSetup.Validate();
            Debug.Log("HOUSE_CIRCUIT_SETUP_PASSED order="+flow.WireOrder+" fuseOffset="+flow.FuseOffset+" fuseDistance="+flow.FuseDistance+" preserved="+snapshots.Count);
        }
        public static void FinalCheck()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            int Count()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>t.GetComponents<Component>().Length));
            int before=Count();Run();if(Count()!=before)throw new Exception("Circuit setup duplicated objects/components");
            Debug.Log("CIRCUIT_IDEMPOTENCE_PASSED");
            HousePowerCheck.Run();
        }
    }
}
