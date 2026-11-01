using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-10)]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private GameObject closedDoor;
        [SerializeField] private GameObject openDoor;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private GameObject popup;
        [SerializeField] private string popupText = "Открыть";
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float arriveTolerance = 0.4f;
        [SerializeField] private float sampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float contactTolerance = 0.15f;
        [SerializeField] private UnityEvent onOpened = new();
        [SerializeField] private DoorOpener opener;
        [SerializeField] private bool chooseReachableSide;
        private bool pending;
        private bool opened;
        private Vector3 destination;
        private int commandFrame;
        private float commandTime;
        private float stoppedSince = -1f;
        public bool IsAimed { get; private set; }
        private Collider[] cachedColliders;
        private PinchOfTheft.Core.AimProbe probe;
        private PinchOfTheft.Core.Billboard billboard;
        private Text popupLabel;
        private string actionText;
        private void Start()
        {
            PinchOfTheft.Core.GameContext.Resolve(this);
            actionText = "E  " + popupText;
            if (PinchOfTheft.Core.GameContext.Instance != null)
            {
                probe = PinchOfTheft.Core.GameContext.Instance.AimProbe;
                PinchOfTheft.Core.GameContext.Instance.PlayerChanged += PlayerChanged;
            }
            cachedColliders = closedDoor != null ? closedDoor.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
            if (popup != null)
            {
                popupLabel = popup.GetComponentInChildren<Text>(true);
                billboard = popup.GetComponent<PinchOfTheft.Core.Billboard>();
                if (billboard == null) billboard = popup.AddComponent<PinchOfTheft.Core.Billboard>();
            }
        }
        public bool IsUnlocked { get; private set; }
        public void Unlock() => IsUnlocked = true;

        private void Update()
        {
            IsAimed = false;
            if (cameraManager == null || mover == null || closedDoor == null ||
                openDoor == null || approachPoint == null)
            {
                SetPopup(false);
                return;
            }
            if (cameraManager.InputLocked || opened)
            {
                pending = false;
                SetPopup(false);
                return;
            }
            var active = cameraManager.ActiveCamera;
            if (active != null && menu != null && !menu.IsOpen)
            {
                if (probe != null && probe.HasHit && probe.Hit.distance <= maxDistance)
                {
                    IsAimed = probe.Collider.GetComponentInParent<InteractableSphere>() == null &&
                        probe.Collider.transform.IsChildOf(closedDoor.transform);
                }
            }
            SetPopup(IsAimed);
            if (IsUnlocked && IsAimed && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                BeginApproach();
            if (!pending || Time.frameCount == commandFrame || Time.time - commandTime < 0.25f) return;
            if (Vector3.Distance(mover.Destination, destination) > 0.2f)
            {
                pending = false;
                return;
            }
            if (!mover.PathPending && IsTouchingDoor())
            {
                opened = true;
                pending = false;
                IsAimed = false;
                if (opener != null) opener.Open();
                else { closedDoor.SetActive(false); openDoor.SetActive(true); }
                SetPopup(false);
                if (marker != null) marker.Hide();
                onOpened.Invoke();
                enabled = false;
            }
            else if (!mover.PathPending && !mover.IsMoving)
            {
                // После остановки агента коллайдер обновляется на следующем шаге физики.
                if (stoppedSince < 0f) stoppedSince = Time.time;
                else if (Time.time - stoppedSince > 0.3f) pending = false;
            }
            else stoppedSince = -1f;
        }
        private void PlayerChanged(PlayerMover player)
        {
            mover = player;
            marker = PinchOfTheft.Core.GameContext.Instance.Marker;
            pending = false;
        }
        private void OnDestroy()
        {
            if (PinchOfTheft.Core.GameContext.Instance != null) PinchOfTheft.Core.GameContext.Instance.PlayerChanged -= PlayerChanged;
        }

        private void BeginApproach()
        {
            if (!IsUnlocked || opened) return;
            if (chooseReachableSide)
            {
                if (cachedColliders == null || cachedColliders.Length == 0) return;
                var agent = mover.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.isOnNavMesh) return;
                Bounds bounds = cachedColliders[0].bounds;
                Vector3 normal = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
                float best = float.PositiveInfinity;
                var path = new NavMeshPath();
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 candidate = bounds.center + normal * (agent.radius + 0.12f) * side;
                    candidate.y = approachPoint.position.y;
                    if (!NavMesh.SamplePosition(candidate, out var sample, sampleRadius, agent.areaMask) ||
                        !agent.CalculatePath(sample.position,path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    var corners = path.corners; float length = 0f;
                    for(int i=1;i<corners.Length;i++) length += Vector3.Distance(corners[i-1],corners[i]);
                    if(length>=best)continue;
                    best=length; destination=sample.position;
                }
                if(float.IsInfinity(best))return;
            }
            else
            {
                if (!NavMesh.SamplePosition(approachPoint.position, out var hit, sampleRadius, NavMesh.AllAreas)) return;
                destination = hit.position;
            }
            mover.MoveTo(destination);
            if (marker != null) marker.Show(destination);
            pending = true;
            commandFrame = Time.frameCount;
            commandTime = Time.time;
            stoppedSince = -1f;
        }

        private bool IsTouchingDoor()
        {
            var capsule = mover.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            Bounds body = capsule.bounds;
            float radius = Mathf.Min(body.extents.x, body.extents.z);
            float halfSegment = Mathf.Max(0f, body.extents.y - radius);
            // Проверяем вдоль капсулы, поскольку её центр расположен выше пола.
            foreach (var collider in cachedColliders)
            {
                if (!collider.enabled || collider.isTrigger) continue;
                for (int i = 0; i <= 4; i++)
                {
                    Vector3 point = body.center + Vector3.up * Mathf.Lerp(-halfSegment, halfSegment, i / 4f);
                    if (Vector3.Distance(point, collider.ClosestPoint(point)) <= radius + contactTolerance)
                        return true;
                }
            }
            return false;
        }

        private void SetPopup(bool visible)
        {
            if (popup == null) return;
            popup.SetActive(visible);
            if (!visible) return;
            if (billboard != null) billboard.enabled = true;
            var text = popupLabel;
            if (text != null) { var value = IsUnlocked ? actionText : "Закрыто"; if (text.text != value) text.text = value; }
        }
    }
}




