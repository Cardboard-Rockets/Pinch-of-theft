using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerMover : MonoBehaviour
    {
        private NavMeshAgent agent;
        private bool warned;
        public Vector3 Destination => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.destination : transform.position;
        public bool PathPending => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.pathPending;
        public bool IsMoving => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance));

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public void MoveTo(Vector3 point)
        {
            if (agent == null || !agent.isActiveAndEnabled) return;
            if (!agent.isOnNavMesh)
            {
                if (!warned)
                {
                    Debug.LogWarning("Player не на NavMesh, запеки NavMesh", this);
                    warned = true;
                }
                return;
            }
            agent.SetDestination(point);
        }
    }
}

