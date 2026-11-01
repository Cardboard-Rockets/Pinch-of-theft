using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class SecondSetup
    {
        private static void Set(Object o,string n,object v){var s=new SerializedObject(o);var p=s.FindProperty(n);if(v is bool b)p.boolValue=b;else p.objectReferenceValue=v as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        private static UnityEvent Event(Object o,string n)=>(UnityEvent)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
        private static void Wire(Object o,string field,UnityAction action){var e=Event(o,field);for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==action.Target as Object&&e.GetPersistentMethodName(i)==action.Method.Name)return;UnityEventTools.AddPersistentListener(e,action);EditorUtility.SetDirty(o);}
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");if(Object.FindAnyObjectByType<GameContext>()==null)throw new Exception("Core missing");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var snapshots=all.ToDictionary(t=>t,t=>(t.name,t.parent,t.localPosition,t.localRotation,t.localScale));
            Transform Unique(string n){var a=all.Where(t=>HouseCircuitAudit.Normalize(t.name)==n).ToArray();if(a.Length!=1)throw new Exception("Candidates "+n+": "+string.Join(",",a.Select(HouseCircuitAudit.Path)));return a[0];}
            var root=Unique("wire2");var first=Unique("wire1").GetComponent<CurrentFlow>();var start=Unique("start2");var finish=Unique("finish2");var current=Unique("sparkle2");var fuses=all.Where(t=>HouseCircuitAudit.Normalize(t.name)=="fu2").ToArray();if(fuses.Length>1)throw new Exception("fu2 ambiguous");
            var flow=root.GetComponent<CurrentFlow>();bool created=flow==null;if(created){flow=root.gameObject.AddComponent<CurrentFlow>();EditorUtility.CopySerialized(first,flow);var s=new SerializedObject(flow);s.FindProperty("wires").arraySize=0;s.ApplyModifiedPropertiesWithoutUndo();}
            Set(flow,"startPoint",start);Set(flow,"finishPoint",finish);Set(flow,"currentObject",current);Set(flow,"wireRoot",root);Set(flow,"fuse",fuses.FirstOrDefault());
            var manager=Object.FindAnyObjectByType<PinchOfTheft.CCTV.CameraManager>();
            InteractableSphere Interact(Transform t,float duration,bool available)=>(InteractableSphere)typeof(HouseCircuitSetup).GetMethod("Interaction",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{t,manager,duration,available});
            var begin=Interact(start,0.3f,false);Set(flow,"startInteractable",begin);Wire(begin,"onCompleted",flow.Begin);
            if(fuses.Length==1)Wire(Interact(fuses[0],1f,true),"onCompleted",flow.ActivateFuse);
            var energies=all.Where(t=>HouseCircuitAudit.Normalize(t.name)=="elec2").ToArray();if(energies.Length>1)throw new Exception("elec2 ambiguous");var energy=energies.Length==1?Interact(energies[0],2f,true):Unique("elec").GetComponent<InteractableSphere>();
            var e=Event(energy,"onCompleted");bool wired=false;for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==flow&&e.GetPersistentMethodName(i)=="SetPowered")wired=true;if(!wired)UnityEventTools.AddBoolPersistentListener(e,flow.SetPowered,true);EditorUtility.SetDirty(energy);
            var label=start.Find("CircuitNoPower2");if(label==null){var source=(GameObject)new SerializedObject(first).FindProperty("noPowerLabel").objectReferenceValue;var go=Object.Instantiate(source,start);go.name="CircuitNoPower2";label=go.transform;var scale=start.lossyScale;label.localScale=new Vector3(0.005f/scale.x,0.005f/scale.y,0.005f/scale.z);label.rotation=Quaternion.identity;var bounds=start.GetComponent<Collider>().bounds;label.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);}
            Set(flow,"noPowerLabel",label.gameObject);current.gameObject.SetActive(false);foreach(var c in current.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var b in current.GetComponentsInChildren<Rigidbody>(true)){b.isKinematic=true;b.useGravity=false;}
            Physics.SyncTransforms();flow.ValidatePath();
            foreach(var x in snapshots){var t=x.Key;var v=x.Value;if(t.name!=v.Item1||t.parent!=v.Item2||t.localPosition!=v.Item3||t.localRotation!=v.Item4||t.localScale!=v.Item5)throw new Exception("Transform changed "+v.Item1);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);PrefabSetup.Validate();Debug.Log("SECOND_CIRCUIT_SETUP order="+flow.WireOrder+" fuseOffset="+flow.FuseOffset+" distance="+flow.FuseDistance);
        }
        public static void LockPanel2(){Lock("panel2");}
        public static void LockKeypad2(){Lock("keypad2");}
        public static void LockNewKeypad(){Lock("newkeypad");}
        public static void FinalCheck()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            int Count()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>t.GetComponents<Component>().Length));
            int before=Count();Run();LockNewKeypad();if(before!=Count())throw new Exception("Second setup duplicated objects/components");
            Debug.Log("SECOND_IDEMPOTENCE_PASSED");SecondCheck.Run();
        }
        public static void LockDoor(){Lock("door");}
        private static void Lock(string selected)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var box=all.Single(t=>HouseCircuitAudit.Normalize(t.name)=="boxx");
            var firstPanel=(InteractableSphere)new SerializedObject(Object.FindObjectsByType<CodePuzzle>().Single()).FindProperty("panelInteractable").objectReferenceValue;
            var target=selected=="newkeypad"?all.Single(t=>HouseCircuitAudit.Normalize(t.name)=="keypad"&&t!=firstPanel.transform):selected=="door"?box.GetComponentsInChildren<Transform>(true).Single(t=>HouseCircuitAudit.Normalize(t.name)=="door"):all.Single(t=>HouseCircuitAudit.Normalize(t.name)==selected);
            var manager=Object.FindAnyObjectByType<PinchOfTheft.CCTV.CameraManager>();var interaction=(InteractableSphere)typeof(HouseCircuitSetup).GetMethod("Interaction",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{target,manager,0.5f,false});Set(interaction,"singleUse",false);
            var codeLock=target.GetComponent<CodeLock>()??target.gameObject.AddComponent<CodeLock>();var source=Object.FindObjectsByType<CodePuzzle>().Single();Set(codeLock,"source",source);Set(codeLock,"keypad",Object.FindAnyObjectByType<KeypadUI>());Set(codeLock,"lockInteractable",interaction);Wire(interaction,"onCompleted",codeLock.Open);
            var generated=Event(source,"onCodeGenerated");bool wired=false;for(int i=0;i<generated.GetPersistentEventCount();i++)if(generated.GetPersistentTarget(i)==interaction)wired=true;if(!wired)UnityEventTools.AddBoolPersistentListener(generated,interaction.SetAvailable,true);EditorUtility.SetDirty(source);
            var doors=box.GetComponentsInChildren<Transform>(true).Where(t=>HouseCircuitAudit.Normalize(t.name)=="door").ToArray();var opened=box.GetComponentsInChildren<Transform>(true).Where(t=>HouseCircuitAudit.Normalize(t.name)=="opendoor").ToArray();
            if(doors.Length==1&&opened.Length==1){var opener=doors[0].GetComponent<DoorOpener>()??doors[0].gameObject.AddComponent<DoorOpener>();Set(opener,"closedDoor",doors[0].gameObject);Set(opener,"openedDoor",opened[0].gameObject);doors[0].gameObject.SetActive(true);opened[0].gameObject.SetActive(false);Wire(codeLock,"onSolved",opener.Open);}
            else Debug.LogWarning("Boxx door candidates: "+string.Join(",",doors.Concat(opened).Select(HouseCircuitAudit.Path)));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);PrefabSetup.Validate();Debug.Log("SECOND_LOCK_SETUP "+HouseCircuitAudit.Path(target));
        }
    }
}

