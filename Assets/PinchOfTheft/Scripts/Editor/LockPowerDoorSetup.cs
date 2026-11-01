using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class LockPowerDoorSetup
    {
        private static void Set(Object o,string n,object value){var s=new SerializedObject(o);var p=s.FindProperty(n);if(value is bool b)p.boolValue=b;else p.objectReferenceValue=value as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var codeLock=Object.FindObjectsByType<CodeLock>().Single();Set(codeLock,"requirePower",true);Set(codeLock,"powered",false);
            var energy=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("elec",StringComparison.OrdinalIgnoreCase));
            var e=(UnityEvent)typeof(InteractableSphere).GetField("onCompleted",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(energy);bool exists=false;for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==codeLock&&e.GetPersistentMethodName(i)=="SetPowered")exists=true;
            if(!exists)UnityEventTools.AddBoolPersistentListener(e,codeLock.SetPowered,true);EditorUtility.SetDirty(energy);
            var source=Object.FindObjectsByType<CodePuzzle>().Single();typeof(HousePowerSetup).GetMethod("Popup",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{codeLock.transform,source});Set(codeLock.GetComponent<NoPowerPrompt>(),"powerLock",codeLock);
            var box=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>HouseCircuitAudit.Normalize(t.name)=="boxx");var door=box.GetComponentsInChildren<Transform>(true).Single(t=>HouseCircuitAudit.Normalize(t.name)=="door");
            var collider=door.GetComponent<BoxCollider>();if(collider==null)collider=door.gameObject.AddComponent<BoxCollider>();var mesh=door.GetComponent<MeshFilter>().sharedMesh;collider.center=mesh.bounds.center;collider.size=mesh.bounds.size;collider.enabled=true;collider.isTrigger=false;
            if(door.gameObject.layer==2)door.gameObject.layer=0;EditorUtility.SetDirty(collider);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);PrefabSetup.Validate();Debug.Log("LOCK_POWER_DOOR_SETUP_PASSED");
        }
    }
}
