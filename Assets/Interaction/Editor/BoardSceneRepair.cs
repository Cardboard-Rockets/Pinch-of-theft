using System.Linq;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Interaction.Editor
{
    public static class BoardSceneRepair
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var board = Object.FindAnyObjectByType<BoardController>();
            var settings = new SerializedObject(board);
            int mask = ~((1 << LayerMask.NameToLayer("Player")) | (1 << 2));
            settings.FindProperty("mask").intValue = mask;
            var standing = (GameObject)settings.FindProperty("standingBoard").objectReferenceValue;
            var point = (Transform)settings.FindProperty("approachPoint").objectReferenceValue;
            var popup = (GameObject)settings.FindProperty("popup").objectReferenceValue;
            var mover = Object.FindAnyObjectByType<PlayerMover>();
            var meshCollider = standing.GetComponent<MeshCollider>();
            if (meshCollider != null) meshCollider.enabled = false;
            var collider = standing.GetComponent<BoxCollider>();
            if (collider == null) collider = standing.AddComponent<BoxCollider>();
            var mesh = standing.GetComponent<MeshFilter>().sharedMesh.bounds;
            collider.center = mesh.center;
            collider.size = new Vector3(Mathf.Max(mesh.size.x, 0.05f), Mathf.Max(mesh.size.y, 0.05f), Mathf.Max(mesh.size.z, 0.05f));
            collider.enabled = true; collider.isTrigger = false;
            Physics.SyncTransforms();
            var bounds = collider.bounds;
            Vector3 best = Vector3.zero; float bestDistance = float.PositiveInfinity;
            var path = new NavMeshPath();
            NavMesh.SamplePosition(mover.transform.position, out var start, 3, NavMesh.AllAreas);
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 surface = collider.ClosestPoint(bounds.center + direction * 10);
                Vector3 candidate = surface + direction * 0.4f;
                candidate.y = start.position.y;
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.3f, NavMesh.AllAreas)) continue;
                Vector3 bodyPoint = hit.position + Vector3.up;
                Vector3 lowerPoint = hit.position + Vector3.up * 0.5f;
                if (Mathf.Min(Vector3.Distance(bodyPoint, collider.ClosestPoint(bodyPoint)), Vector3.Distance(lowerPoint, collider.ClosestPoint(lowerPoint))) > 0.5f) continue;
                if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float distance = Vector3.Distance(start.position, hit.position);
                if (distance >= bestDistance) continue;
                bestDistance = distance; best = hit.position;
            }
            if (float.IsInfinity(bestDistance)) throw new System.Exception("No reachable board contact point");
            point.position = best;
            popup.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 0.35f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            // Исправляем тот же пустой LayerMask у общего взаимодействия.
            var interactor = new SerializedObject(Object.FindAnyObjectByType<SphereInteractor>());
            interactor.FindProperty("interactMask").intValue = mask; interactor.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("BOARD_REPAIR_COMPLETE " + best);
        }
    }
}


