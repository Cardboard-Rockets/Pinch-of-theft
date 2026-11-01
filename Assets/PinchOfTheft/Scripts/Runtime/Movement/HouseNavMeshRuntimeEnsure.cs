using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace PinchOfTheft.Movement
{
    /// <summary>
    /// Keeps House playable when a checked-in NavMeshData asset is missing or cannot be loaded.
    /// The serialized bake is still used when valid; rebuilding is only a fallback.
    /// </summary>
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class HouseNavMeshRuntimeEnsure : MonoBehaviour
    {
        private NavMeshSurface surface;

        private void Awake()
        {
            surface = GetComponent<NavMeshSurface>();
            if (HasValidNavMesh()) return;

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (!HasValidNavMesh())
                Debug.LogError("House: NavMesh не удалось загрузить или пересобрать.", this);
            else
                Debug.LogWarning("House: NavMeshData отсутствовал или был повреждён; сетка пересобрана автоматически.", this);
        }

        private static bool HasValidNavMesh()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            return triangulation.vertices != null && triangulation.indices != null && triangulation.indices.Length >= 3;
        }
    }
}
