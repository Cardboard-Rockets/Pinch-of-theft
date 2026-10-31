using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PinchOfTheft.Editor
{
    public static class HouseCircuitAudit
    {
        public static string Normalize(string n)=>n.Replace(" ","").Replace("_","").ToLowerInvariant();
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach(var name in new[]{"start","finish","elec","fu1","sparkle1","wire1"})foreach(var t in all.Where(t=>Normalize(t.name)==name))
            {
                Debug.Log("CIRCUIT_OBJECT "+name+" path="+Path(t)+" position="+t.position+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name)));
                if(name=="wire1")foreach(var child in t.GetComponentsInChildren<Transform>(true)){var m=child.GetComponent<MeshFilter>();if(m!=null)Debug.Log("CIRCUIT_WIRE "+Path(child)+" position="+child.position+" scale="+child.lossyScale+" bounds="+(m.sharedMesh!=null?m.sharedMesh.bounds.ToString():"null"));}
            }
        }
        public static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
    }
}
