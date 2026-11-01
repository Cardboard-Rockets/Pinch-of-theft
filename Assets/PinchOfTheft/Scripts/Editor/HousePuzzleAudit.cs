using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PinchOfTheft.Editor
{
    public static class HousePuzzleAudit
    {
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)))
                if(System.Text.RegularExpressions.Regex.IsMatch(t.name,"tv|тв|телев|панел|panel|keypad|двер|door|screen|экран|lock",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    Debug.Log("PUZZLE_OBJECT: "+Path(t)+" position="+t.position+" scale="+t.lossyScale+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name)));
                    foreach(var r in t.GetComponentsInChildren<Renderer>(true))Debug.Log("PUZZLE_RENDERER: "+Path(r.transform)+" bounds="+r.bounds+" local="+r.localBounds);
                }
        }
        private static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
    }
}
