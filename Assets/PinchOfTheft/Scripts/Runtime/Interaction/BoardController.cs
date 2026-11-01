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
    public sealed class BoardController : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private GameObject standingBoard;
        [SerializeField] private GameObject fallenBoard;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private GameObject popup;
        [SerializeField] private string popupText = "Уронить";
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float arriveTolerance = 0.4f;
        [SerializeField] private float sampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float contactTolerance = 0.15f;
        [SerializeField] private bool requireContact = true;
        [SerializeField] private UnityEvent onFallen = new();
        private bool pending;
        private bool fallen;
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
            cachedColliders = standingBoard != null ? standingBoard.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
            if (cameraManager == null || mover == null || standingBoard == null || fallenBoard == null || approachPoint == null)
                Debug.LogWarning("BoardController: назначь standingBoard, fallenBoard, approachPoint и PlayerMover", this);
            if (popup != null)
            {
                popupLabel = popup.GetComponentInChildren<Text>(true);
                billboard = popup.GetComponent<PinchOfTheft.Core.Billboard>();
                if (billboard == null) billboard = popup.AddComponent<PinchOfTheft.Core.Billboard>();
            }
        }

        private void Update()
        {
            IsAimed = false;
            if (cameraManager == null || mover == null || standingBoard == null ||
                fallenBoard == null || approachPoint == null)
            {
                SetPopup(false);
                return;
            }
            if (cameraManager.InputLocked || fallen)
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
                        probe.Collider.transform.IsChildOf(standingBoard.transform);
                }
            }
            SetPopup(IsAimed);
            if (IsAimed && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                BeginApproach();
            if (!pending || Time.frameCount == commandFrame || Time.time - commandTime < 0.25f) return;
            if (Vector3.Distance(mover.Destination, destination) > 0.2f)
            {
                pending = false;
                return;
            }
            Vector3 remaining = mover.transform.position - approachPoint.position;
            remaining.y = 0f;
            if (!mover.PathPending && (requireContact ? IsTouchingBoard() : remaining.sqrMagnitude <= arriveTolerance * arriveTolerance))
            {
                fallen = true;
                pending = false;
                IsAimed = false;
                standingBoard.SetActive(false);
                fallenBoard.SetActive(true);
                SetPopup(false);
                if (marker != null) marker.Hide();
                onFallen.Invoke();
                enabled = false;
            }
            else if (!mover.PathPending && !mover.IsMoving)
            {
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
            if (!NavMesh.SamplePosition(approachPoint.position, out var hit, sampleRadius, NavMesh.AllAreas)) return;
            destination = hit.position;
            mover.MoveTo(destination);
            if (marker != null) marker.Show(destination);
            pending = true;
            commandFrame = Time.frameCount;
            commandTime = Time.time;
            stoppedSince = -1f;
        }

        private bool IsTouchingBoard()
        {
            var capsule = mover.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            Bounds body = capsule.bounds;
            float radius = Mathf.Min(body.extents.x, body.extents.z);
            float halfSegment = Mathf.Max(0f, body.extents.y - radius);
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
            if (text != null && text.text != actionText) text.text = actionText;
        }
    }
}



