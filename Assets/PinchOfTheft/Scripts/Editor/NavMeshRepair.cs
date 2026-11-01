using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    public static class NavMeshRepair
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface == null) throw new System.Exception("House NavMeshSurface не найден");
            Physics.SyncTransforms();
            surface.RemoveData();
            surface.BuildNavMesh();
            var built = surface.navMeshData;
            if (built == null) throw new System.Exception("NavMesh не построен");
            var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Scenes/HouseNavMesh.asset");
            if (asset == null) { asset = new NavMeshData(); AssetDatabase.CreateAsset(asset, "Assets/Scenes/HouseNavMesh.asset"); }
            surface.RemoveData();
            EditorUtility.CopySerialized(built, asset);
            surface.navMeshData = asset;
            surface.AddData();
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(asset);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("NAVMESH_REPAIRED bytes=" + new System.IO.FileInfo("Assets/Scenes/HouseNavMesh.asset").Length + " triangles=" + NavMesh.CalculateTriangulation().indices.Length / 3);
        }
    }
}
