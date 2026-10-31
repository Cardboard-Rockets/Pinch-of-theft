using System.Linq;
using System.Reflection;
using PinchOfTheft.CCTV;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction.Editor
{
    public static class CurrentSceneSetup
    {
        private static string Name(string value)=>string.Concat(value.Where(c=>!char.IsWhiteSpace(c))).ToLowerInvariant();
        private static void Set(Object obj,string field,Object value){var s=new SerializedObject(obj);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var all=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var start=all.Where(t=>new[]{"start","старт","начало"}.Contains(Name(t.name))).ToArray();
            var finish=all.Where(t=>new[]{"finish","финиш","конец"}.Contains(Name(t.name))).ToArray();
            var current=all.Where(t=>new[]{"current","electrocurrent","electriccurrent","электроток","электрическийток","ток"}.Contains(Name(t.name))).ToArray();
            if(start.Length!=1 || finish.Length!=1 || current.Length!=1)
                throw new System.Exception("Ambiguous chain: Start="+string.Join(",",start.Select(t=>t.name))+" Finish="+string.Join(",",finish.Select(t=>t.name))+" Current="+string.Join(",",current.Select(t=>t.name)));
            if(start[0].parent==null || start[0].parent!=finish[0].parent || start[0].parent!=current[0].parent)throw new System.Exception("Chain objects have different parents");
            var flow=start[0].parent.gameObject.AddComponent<CurrentFlow>();
            Set(flow,"startPoint",start[0]);Set(flow,"finishPoint",finish[0]);Set(flow,"currentObject",current[0]);
            var manager=Object.FindAnyObjectByType<CameraManager>();
            var interaction=start[0].gameObject.AddComponent<InteractableSphere>();Set(interaction,"cameraManager",manager);
            var config=new SerializedObject(interaction);config.FindProperty("unlockGroup").intValue=-1;config.FindProperty("holdDuration").floatValue=0.3f;config.FindProperty("singleUse").boolValue=true;config.FindProperty("startAvailable").boolValue=true;config.ApplyModifiedPropertiesWithoutUndo();
            start[0].gameObject.layer=LayerMask.NameToLayer("Interactable");
            if(start[0].GetComponent<Collider>()==null)start[0].gameObject.AddComponent<BoxCollider>();
            foreach(var collider in start[0].GetComponentsInChildren<Collider>()){collider.enabled=true;collider.isTrigger=false;}
            var shield=all.Single(t=>Name(t.name)=="shield");var original=shield.GetComponentInChildren<KeyPrompt>(true);
            var prompt=Object.Instantiate(original,start[0],false);prompt.name="KeyPrompt";prompt.gameObject.SetActive(true);prompt.transform.localScale=Vector3.one*0.005f;
            var renderer=start[0].GetComponent<Renderer>();var bounds=renderer!=null?renderer.bounds:new Bounds(start[0].position,Vector3.one);
            prompt.transform.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);Set(interaction,"prompt",prompt);prompt.SetProgress(0);prompt.SetHighlighted(false);
            var completed=(UnityEvent)typeof(InteractableSphere).GetField("onCompleted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(interaction);UnityEventTools.AddPersistentListener(completed,flow.Begin);EditorUtility.SetDirty(interaction);
            foreach(var collider in current[0].GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in current[0].GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
            flow.ValidatePath();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("CURRENT_SETUP_COMPLETE: "+flow.WireOrder);
        }
    }
}
