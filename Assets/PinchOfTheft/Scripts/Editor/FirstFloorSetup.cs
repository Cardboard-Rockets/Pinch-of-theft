using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class FirstFloorSetup
    {
        private static NavMeshModifierVolume Volume(Transform root,string name,Bounds bounds)
        {
            var found=root.Find(name);var go=found!=null?found.gameObject:new GameObject(name);go.transform.SetParent(root,false);go.transform.position=bounds.center;
            var v=go.GetComponent<NavMeshModifierVolume>();if(v==null)v=go.AddComponent<NavMeshModifierVolume>();v.center=Vector3.zero;v.size=bounds.size;v.area=1;return v;
        }
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var snapshots=all.ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            var context=Object.FindAnyObjectByType<GameContext>();if(context==null)throw new Exception("No Core");if(context.GetComponent<DoorPassageCollisions>()==null)context.gameObject.AddComponent<DoorPassageCollisions>();
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");if(LayerMask.NameToLayer("DoorPassage")<0){bool added=false;for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue="DoorPassage";added=true;break;}if(!added)throw new Exception("No free layer");tags.ApplyModifiedPropertiesWithoutUndo();}
            int doorLayer=LayerMask.NameToLayer("DoorPassage"),count=0;
            var probeSettings=new SerializedObject(context.AimProbe);probeSettings.FindProperty("mask").intValue|=1<<doorLayer;probeSettings.ApplyModifiedPropertiesWithoutUndo();
            foreach(var click in Object.FindObjectsByType<ClickToMoveInput>()){var s=new SerializedObject(click);s.FindProperty("raycastMask").intValue|=1<<doorLayer;s.ApplyModifiedPropertiesWithoutUndo();}
            foreach(var t in all)
            {
                string name=t.name.Trim().ToLowerInvariant();if(name!="door"&&name!="open_door"&&name!="movable")continue;
                var renderers=t.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)continue;var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);if(bounds.center.y>=3.8f)continue;
                foreach(var collider in t.GetComponentsInChildren<Collider>(true))collider.gameObject.layer=doorLayer;
                var modifier=t.GetComponent<NavMeshModifier>();if(modifier==null)modifier=t.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
                foreach(var obstacle in t.GetComponentsInChildren<NavMeshObstacle>(true))obstacle.enabled=false;
                count++;Debug.Log("FIRST_FLOOR_DOOR "+HouseCircuitAudit.Path(t));
            }
            var service=all.FirstOrDefault(t=>t.name=="FirstFloorNavigation");if(service==null)service=new GameObject("FirstFloorNavigation").transform;
            Volume(service,"UpperFloorsBlocked",new Bounds(new Vector3(0,502.9f,0),new Vector3(2000,1000,2000)));
            int stairs=0;
            foreach(var t in all.Where(t=>t.name.IndexOf("stairs",StringComparison.OrdinalIgnoreCase)>=0))
            {
                var renderers=t.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)continue;var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);if(bounds.min.y>3.8f)continue;
                bounds.Expand(new Vector3(0.55f,0.2f,0.55f));Volume(service,"StairsBlocked_"+stairs,bounds);stairs++;
            }
            var surface=Object.FindObjectsByType<NavMeshSurface>().Single();surface.BuildNavMesh();var built=surface.navMeshData;var data=AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Scenes/HouseNavMesh.asset");surface.RemoveData();EditorUtility.CopySerialized(built,data);surface.navMeshData=data;surface.AddData();Object.DestroyImmediate(built);EditorUtility.SetDirty(data);
            foreach(var pair in snapshots){var t=pair.Key;var v=pair.Value;if(t.localPosition!=v.Item1||t.localRotation!=v.Item2||t.localScale!=v.Item3)throw new Exception("Moved "+t.name);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();PrefabSetup.Validate();Debug.Log("FIRST_FLOOR_SETUP_PASSED doors="+count+" stairVolumes="+stairs+" triangles="+NavMesh.CalculateTriangulation().indices.Length/3);
        }
    }
}
