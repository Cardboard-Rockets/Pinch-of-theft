using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class PlayerCollisionGuard : MonoBehaviour
    {
        [SerializeField, Tooltip("Твёрдые препятствия, останавливающие персонажа")] private LayerMask obstacleMask = ~0;
        [SerializeField, Min(0.001f), Tooltip("Зазор между капсулой и препятствием")] private float skinWidth = 0.02f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private NavMeshAgent agent;
        private CapsuleCollider capsule;
        private Vector3 lastPosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            capsule = GetComponent<CapsuleCollider>();
        }

        private void OnEnable() => lastPosition = transform.position;

        private void LateUpdate()
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh || !capsule.enabled)
            {
                lastPosition = transform.position;
                return;
            }
            Vector3 position = transform.position;
            Vector3 movement = position - lastPosition;
            float distance = movement.magnitude;
            if (distance < 0.00001f) return;
            Bounds bounds = capsule.bounds;
            float fullRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            float radius = Mathf.Max(0.01f, fullRadius - skinWidth);
            float halfSegment = Mathf.Max(0f, bounds.extents.y - fullRadius);
            Vector3 center = bounds.center - movement;
            Vector3 direction = movement / distance;
            int count = Physics.CapsuleCastNonAlloc(center + Vector3.up * halfSegment,
                center - Vector3.up * halfSegment, radius, direction, hits,
                distance + skinWidth, obstacleMask, QueryTriggerInteraction.Ignore);
            float allowed = distance;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer)) continue;
                // Пол и допустимые склоны остаются под управлением NavMeshAgent.
                if (Vector3.Dot(hit.normal, Vector3.up) > 0.5f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - skinWidth));
                blocked = true;
            }
            if (blocked)
            {
                Vector3 safePosition = lastPosition + direction * allowed;
                if (agent.Warp(safePosition)) agent.ResetPath();
            }
            lastPosition = transform.position;
        }
    }
}
