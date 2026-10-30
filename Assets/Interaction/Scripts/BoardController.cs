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
        [SerializeField] private UnityEvent onFallen = new();
        private bool pending;
        private bool fallen;
        private Vector3 destination;
        private int commandFrame;
        private float commandTime;
        public bool IsAimed { get; private set; }

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
                Camera camera = active.GetComponent<Camera>();
                if (camera != null)
                {
                    Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
                    if (Physics.Raycast(ray, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
                        IsAimed = hit.collider.GetComponentInParent<InteractableSphere>() == null &&
                            hit.collider.transform.IsChildOf(standingBoard.transform);
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
            if (!mover.PathPending && IsTouchingBoard())
            {
                fallen = true;
                pending = false;
                IsAimed = false;
                standingBoard.SetActive(false);
                fallenBoard.SetActive(true);
                SetPopup(false);
                if (marker != null) marker.Hide();
                onFallen.Invoke();
            }
            else if (!mover.PathPending && !mover.IsMoving) pending = false;
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
        }

        private bool IsTouchingBoard()
        {
            var capsule = mover.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            Bounds body = capsule.bounds;
            float radius = Mathf.Min(body.extents.x, body.extents.z);
            float halfSegment = Mathf.Max(0f, body.extents.y - radius);
            // Проверяем вдоль капсулы, поскольку её центр расположен выше пола.
            foreach (var collider in standingBoard.GetComponentsInChildren<Collider>())
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
            popup.transform.rotation = cameraManager.ActiveCamera.transform.rotation;
            var text = popup.GetComponentInChildren<Text>();
            if (text != null) text.text = "E  " + popupText;
        }
    }
}



