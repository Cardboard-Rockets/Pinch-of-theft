using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PinchOfTheft.Editor
{
    public static class DamekAudit
    {
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>new[]{"damek","houseroad3","house"}.Contains(t.name.Replace(" ","").ToLowerInvariant())))
            {
                Debug.Log("DAMEK_AUDIT "+t.name+" position="+t.position+" rotation="+t.eulerAngles+" scale="+t.lossyScale+" up="+t.up);
                foreach(var r in t.GetComponentsInChildren<Renderer>(true))Debug.Log("DAMEK_GEOMETRY "+t.name+"/"+r.name+" bounds="+r.bounds+" colliders="+string.Join(",",r.GetComponents<Collider>().Select(c=>c.GetType().Name)));
                Debug.Log("DAMEK_COMPONENTS "+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name)));
            }
        }
    }
}
