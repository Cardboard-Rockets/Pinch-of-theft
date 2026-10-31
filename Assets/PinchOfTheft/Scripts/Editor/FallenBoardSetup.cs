using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Interaction.Editor
{
    public static class FallenBoardSetup
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var controller = Object.FindAnyObjectByType<BoardController>();
            var settings = new SerializedObject(controller);
            var standing = (GameObject)settings.FindProperty("standingBoard").objectReferenceValue;
            var fallen = (GameObject)settings.FindProperty("fallenBoard").objectReferenceValue;
            if (fallen == null)
            {
                fallen = Object.Instantiate(standing, standing.transform.parent);
                fallen.name = "FallenBoard";
                settings.FindProperty("fallenBoard").objectReferenceValue = fallen;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            var sourceFilter = standing.GetComponent<MeshFilter>();
            var sourceRenderer = standing.GetComponent<MeshRenderer>();
            fallen.GetComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            var renderer = fallen.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            renderer.enabled = true;
            fallen.transform.localScale = standing.transform.localScale;
            Vector3 size = sourceFilter.sharedMesh.bounds.size;
            Vector3 normal = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            fallen.transform.rotation = Quaternion.FromToRotation(standing.transform.TransformDirection(normal), Vector3.up) * standing.transform.rotation;
            Vector3 away = Vector3.ProjectOnPlane(standing.transform.forward, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.1f) away = Vector3.forward;
            Vector3 center = sourceRenderer.bounds.center;
            fallen.transform.position = center + away * Mathf.Max(sourceRenderer.bounds.extents.y, 0.5f);
            var approach = (Transform)settings.FindProperty("approachPoint").objectReferenceValue;
            float floor = approach != null ? approach.position.y : 0;
            fallen.SetActive(true);
            fallen.transform.position += Vector3.up * (floor + 0.025f - renderer.bounds.min.y);
            foreach (var collider in fallen.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var obstacle in fallen.GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = false;
            fallen.SetActive(false);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FALLEN_BOARD_READY: same mesh/material, flat on floor, no collision or carving");
        }
    }
}
