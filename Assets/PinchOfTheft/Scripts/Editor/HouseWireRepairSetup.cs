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
using UnityEngine.AI;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    public static class HouseWireRepairSetup
    {
        private static void Set(Object o, string field, object value)
        {
            var s = new SerializedObject(o); var p = s.FindProperty(field);
            if (value is bool b) p.boolValue = b;
            else if (value is string text) p.stringValue = text;
            else if (value is float f) p.floatValue = f;
            else p.objectReferenceValue = value as Object;
            s.ApplyModifiedPropertiesWithoutUndo();
        }
        private static UnityEvent Event(Object o, string field) => (UnityEvent)o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        private static void Wire(Object source, string field, Object target, UnityAction<bool> callback)
        {
            var e = Event(source, field);
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
                if (e.GetPersistentTarget(i) == target && e.GetPersistentMethodName(i) == "SetAvailable") return;
            UnityEventTools.AddBoolPersistentListener(e, callback, true); EditorUtility.SetDirty(source);
        }
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var snapshots = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
            Transform Find(string n) => all.Single(t => t.name.Equals(n, StringComparison.OrdinalIgnoreCase));
            var ctx = Object.FindAnyObjectByType<GameContext>(); if (ctx == null) throw new Exception("Core отсутствует");
            var broken = Find("bwire"); var repaired = Find("newwire"); var sparkle = Find("sparkle");
            var onlocker = Find("onlock onlocker").GetComponent<InteractableSphere>();
            var unlock = Find("Unlock cam21").GetComponent<InteractableSphere>();
            var camera = Object.FindObjectsByType<CCTVCamera>().Single(c => c.Group == 1 && c.Slot == 4);
            var service = broken.parent.Find("WireRepair");
            if (service == null) { service = new GameObject("WireRepair").transform; service.SetParent(broken.parent, false); }
            var controller = service.GetComponent<BoardController>(); if (controller == null) controller = service.gameObject.AddComponent<BoardController>();
            Set(controller,"cameraManager",ctx.CameraManager); Set(controller,"menu",ctx.Menu);
            Set(controller,"mover",Find("Damek").GetComponent<PinchOfTheft.Movement.PlayerMover>());
            Set(controller,"standingBoard",broken.gameObject); Set(controller,"fallenBoard",repaired.gameObject);
            Set(controller,"activeEffect",sparkle.gameObject); Set(controller,"requiredCamera",camera);
            Set(controller,"startAvailable",false); Set(controller,"requireContact",false); Set(controller,"hideStandingOnComplete",false);
            Set(controller,"popupText","Починить"); Set(controller,"arriveTolerance",.4f); Set(controller,"maxDistance",15f);
            var approach = service.Find("ApproachPoint");
            if (approach == null)
            {
                var bounds = broken.GetComponent<Renderer>().bounds;
                if (!NavMesh.SamplePosition(bounds.center,out var floor,3f,NavMesh.AllAreas)) throw new Exception("Нет пола под bwire");
                var player = Find("Damek"); var radius = player.GetComponent<NavMeshAgent>().radius;
                float best = float.PositiveInfinity; Vector3 chosen = default;
                for (int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4; var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    var candidate=new Vector3(bounds.center.x,floor.position.y,bounds.center.z)+dir*(radius+.5f+Mathf.Max(bounds.extents.x,bounds.extents.z));
                    if(!NavMesh.SamplePosition(candidate,out var hit,1f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-floor.position.y)>.4f)continue;
                    float distance=Vector3.Distance(player.position,hit.position); if(distance>=best)continue; best=distance;chosen=hit.position;
                }
                if(float.IsInfinity(best))throw new Exception("Нет точки подхода к bwire");
                approach=new GameObject("ApproachPoint").transform;approach.SetParent(service,false);approach.position=chosen;
            }
            Set(controller,"approachPoint",approach);
            var popup=service.Find("Popup");
            if(popup==null)
            {
                var create=typeof(HousePropSetup).GetMethod("CreatePopup",BindingFlags.Static|BindingFlags.NonPublic);
                popup=(Transform)create.Invoke(null,new object[]{service,broken.GetComponent<Renderer>().bounds,"Починить"});
            }
            popup.gameObject.SetActive(false);Set(controller,"popup",popup.gameObject);
            sparkle.gameObject.SetActive(false);repaired.gameObject.SetActive(false);
            foreach(var collider in sparkle.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            var old=Event(onlocker,"onCompleted");for(int i=old.GetPersistentEventCount()-1;i>=0;i--)
                if(old.GetPersistentTarget(i)==unlock&&old.GetPersistentMethodName(i)=="SetAvailable")UnityEventTools.RemovePersistentListener(old,i);
            Wire(onlocker,"onCompleted",controller,controller.SetAvailable);Wire(controller,"onFallen",unlock,unlock.SetAvailable);
            Set(unlock,"startAvailable",false);
            foreach(var pair in snapshots) if(pair.Key.localPosition!=pair.Value.Item1||pair.Key.localRotation!=pair.Value.Item2||pair.Key.localScale!=pair.Value.Item3)
                throw new Exception("Изменён Transform "+pair.Key.name);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("WIRE_REPAIR_APPROACH "+approach.position.ToString("F3"));PrefabSetup.Validate();Debug.Log("WIRE_REPAIR_SETUP_PASSED");
        }
    }
}
