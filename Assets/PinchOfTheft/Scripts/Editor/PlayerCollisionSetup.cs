using PinchOfTheft.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Editor
{
    public static class PlayerCollisionSetup
    {
        private static void Configure(PlayerMover player)
        {
            var collider = player.GetComponent<CapsuleCollider>();
            if (collider == null) collider = player.gameObject.AddComponent<CapsuleCollider>();
            collider.enabled = true; collider.isTrigger = false;
            collider.direction = 1; collider.height = 2f; collider.radius = 0.5f;
            var body = player.GetComponent<Rigidbody>();
            if (body == null) body = player.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var agent = player.GetComponent<NavMeshAgent>();
            agent.radius = 0.5f; agent.height = 2f; agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if (player.GetComponent<PlayerCollisionGuard>() == null) player.gameObject.AddComponent<PlayerCollisionGuard>();
        }

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            Configure(Object.FindAnyObjectByType<PlayerMover>());
            // Эти модели меняют состояние: сетка под ними должна восстанавливаться без перепекания.
            foreach (string name in new[] { "board", "door" })
            {
                var obj = GameObject.Find(name);
                if (obj == null) continue;
                var modifier = obj.GetComponent<NavMeshModifier>();
                if (modifier == null) modifier = obj.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
            }
            Physics.SyncTransforms();
            foreach (var surface in Object.FindObjectsByType<NavMeshSurface>())
            {
                var previous = surface.navMeshData;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();
                var built = surface.navMeshData;
                if (previous != null && AssetDatabase.Contains(previous))
                {
                    surface.RemoveData();
                    EditorUtility.CopySerialized(built, previous);
                    surface.navMeshData = previous;
                    EditorUtility.SetDirty(previous);
                    surface.AddData();
                    Object.DestroyImmediate(built);
                }
                else if (built != null) AssetDatabase.CreateAsset(built, "Assets/Scenes/PlayerCollisionNavMesh.asset");
            }
            EditorSceneManager.SaveScene(scene);
            const string path = "Assets/PinchOfTheft/Prefabs/Player/Player.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            Configure(root.GetComponentInChildren<PlayerMover>(true));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_COLLISION_SETUP_COMPLETE");
        }
    }
}
