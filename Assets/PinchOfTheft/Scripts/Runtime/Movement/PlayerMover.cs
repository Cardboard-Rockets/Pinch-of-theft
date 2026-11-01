using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Дополнительный отступ цели ПКМ от препятствий, в метрах")] private float destinationClearance = 0.12f;
        [SerializeField, Min(0.25f), Tooltip("Радиус поиска свободной цели возле места клика")] private float destinationSearchRadius = 3f;
        [SerializeField, Tooltip("Препятствия, возле которых нужен отступ цели")] private LayerMask destinationObstacleMask = ~0;
        private readonly Collider[] overlaps = new Collider[64];
        private NavMeshPath destinationPath;
        private NavMeshAgent agent;
        private bool warned;
        private void OnEnable()
        {
            if (PinchOfTheft.Core.GameContext.Instance != null) PinchOfTheft.Core.GameContext.Instance.Register(this);
        }
        private void OnDisable()
        {
            PinchOfTheft.Core.GameContext.Audio?.SetRobotMoving(false);
            var context = PinchOfTheft.Core.GameContext.Instance;
            if (context != null && context.PlayerMover == this) context.Register(null);
        }
        public Vector3 Destination => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.destination : transform.position;
        public bool PathPending => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.pathPending;
        public bool IsMoving => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance));

        private void Awake() => agent = GetComponent<NavMeshAgent>();
        private void Start()
        {
            if (agent != null && !agent.isOnNavMesh) WarnOffMesh();
        }

        private void Update() => PinchOfTheft.Core.GameContext.Audio?.SetRobotMoving(IsMoving);
        private void WarnOffMesh()
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(gameObject.name + " не на NavMesh, запеки NavMesh", this);
        }

        public bool TryMoveToWithClearance(Vector3 point, float sampleRadius, out Vector3 destination)
        {
            destination = point;
            if (agent == null || !agent.isActiveAndEnabled) return false;
            if (!agent.isOnNavMesh) { WarnOffMesh(); return false; }
            if (destinationPath == null) destinationPath = new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            float bestScore = float.PositiveInfinity;
            bool found = false;
            int rings = Mathf.CeilToInt(destinationSearchRadius / 0.25f);
            for (int ring = 0; ring <= rings; ring++)
            {
                int samples = ring == 0 ? 1 : 24;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector3 candidate = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 0.25f);
                    if (!NavMesh.SamplePosition(candidate, out var hit, sampleRadius, filter)) continue;
                    if (!HasDestinationClearance(hit.position)) continue;
                    if (!agent.CalculatePath(hit.position, destinationPath) || destinationPath.status != NavMeshPathStatus.PathComplete) continue;
                    if (ring == 0)
                    {
                        destination = hit.position;
                        return agent.SetDestination(destination);
                    }
                    var corners = destinationPath.corners;
                    float length = 0f;
                    for (int j = 1; j < corners.Length; j++) length += Vector3.Distance(corners[j - 1], corners[j]);
                    float score = Vector3.Distance(point, hit.position) + length * 0.01f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    destination = hit.position;
                    found = true;
                }
                if (found && ring * 0.25f > bestScore + sampleRadius) break;
            }
            return found && agent.SetDestination(destination);
        }

        private bool HasDestinationClearance(Vector3 point)
        {
            float radius = agent.radius + destinationClearance;
            // Нижняя точка приподнята над полом: проверяем препятствия, а не землю.
            Vector3 bottom = point + Vector3.up * (radius + 0.05f);
            Vector3 top = point + Vector3.up * Mathf.Max(radius + 0.05f, agent.height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
                destinationObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var obstacle = overlaps[i];
                if (obstacle.transform.IsChildOf(transform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer)) continue;
                return false;
            }
            return true;
        }

        public void MoveTo(Vector3 point)
        {
            if (agent == null || !agent.isActiveAndEnabled) return;
            if (!agent.isOnNavMesh)
            {
                WarnOffMesh();
                return;
            }
            agent.SetDestination(point);
            PinchOfTheft.Core.GameContext.Audio?.PlayRobotCall();
        }
    }
}

