using PinchOfTheft.CCTV;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement.Editor
{
    public static class MovementSceneSetup
    {
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            int layer = LayerMask.NameToLayer("Player");
            if (layer < 0)
                for (int i = 8; i < 32; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    { layers.GetArrayElementAtIndex(i).stringValue = "Player"; layer = i; break; }
            if (layer < 0) throw new System.Exception("No free layer");
            tags.ApplyModifiedPropertiesWithoutUndo();
            var manager = Object.FindAnyObjectByType<CameraManager>();
            var menu = Object.FindAnyObjectByType<CameraMenuUI>();
            var player = GameObject.Find("Capsule") ?? GameObject.Find("Player") ?? GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.layer = layer;
            player.transform.localScale = Vector3.one;
            var collider = (player.TryGetComponent<CapsuleCollider>(out var existingCapsuleCollider) ? existingCapsuleCollider : player.AddComponent<CapsuleCollider>());
            collider.height = 2; collider.radius = 0.5f;
            var body = (player.TryGetComponent<Rigidbody>(out var existingRigidbody) ? existingRigidbody : player.AddComponent<Rigidbody>());
            body.isKinematic = true; body.useGravity = false;
            var agent = (player.TryGetComponent<NavMeshAgent>(out var existingNavMeshAgent) ? existingNavMeshAgent : player.AddComponent<NavMeshAgent>());
            agent.radius = 0.5f; agent.height = 2; agent.baseOffset = 1;
            agent.speed = 3.5f; agent.angularSpeed = 360; agent.acceleration = 12; agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            var mover = (player.TryGetComponent<PlayerMover>(out var existingPlayerMover) ? existingPlayerMover : player.AddComponent<PlayerMover>());
            var cam = GameObject.Find("Cam_01").transform;
            Vector3 center = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 5;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Movement Ground";
            ground.transform.position = new Vector3(center.x, -0.05f, center.z);
            ground.transform.localScale = new Vector3(3, 1, 3);
            ground.isStatic = true;
            var existingGround = GameObject.Find("Plane");
            if (existingGround != null) ground.GetComponent<Renderer>().sharedMaterial = existingGround.GetComponent<Renderer>().sharedMaterial;
            var obstacles = new GameObject("Movement Obstacles");
            Vector3[] offsets = { new(-2.25f, 1, 3), new(2.25f, 1, 3), new(-6, 1, -3), new(6, 1, -3), new(-6, 1, 7), new(6, 1, 7) };
            foreach (var offset in offsets)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Static Obstacle";
                cube.transform.SetParent(obstacles.transform);
                cube.transform.position = ground.transform.position + offset;
                cube.transform.localScale = new Vector3(3, 2, 2);
                cube.isStatic = true;
                cube.GetComponent<Renderer>().sharedMaterial = ground.GetComponent<Renderer>().sharedMaterial;
            }
            var markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = "ClickMarker";
            Object.DestroyImmediate(markerObject.GetComponent<Collider>());
            markerObject.transform.localScale = new Vector3(0.4f, 0.025f, 0.4f);
            var material = new Material(ground.GetComponent<Renderer>().sharedMaterial);
            material.color = Color.yellow;
            AssetDatabase.CreateAsset(material, "Assets/Movement/ClickMarker.mat");
            markerObject.GetComponent<Renderer>().sharedMaterial = material;
            var marker = markerObject.AddComponent<ClickMarker>();
            SetReference(marker, "mover", mover);
            markerObject.SetActive(false);
            var surface = new GameObject("NavMesh").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.layerMask = ~(1 << layer);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true; surface.voxelSize = 0.08f;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/Movement/MovementNavMesh.asset");
            if (!NavMesh.SamplePosition(ground.transform.position, out var hit, 5, NavMesh.AllAreas))
                throw new System.Exception("Player spawn missing NavMesh");
            player.transform.position = hit.position + Vector3.up;
            var input = new GameObject("ClickToMove").AddComponent<ClickToMoveInput>();
            SetReference(input, "cameraManager", manager); SetReference(input, "menu", menu);
            SetReference(input, "mover", mover); SetReference(input, "marker", marker);
            var serialized = new SerializedObject(input);
            serialized.FindProperty("raycastMask").intValue = ~(1 << layer);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MOVEMENT_SETUP_COMPLETE");
        }

        private static void SetReference(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}


