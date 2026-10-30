using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace PinchOfTheft.Movement
{
    public sealed class ClickToMoveInput : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField, Min(0f)] private float maxDistance = 100f;
        [SerializeField, Min(0f)] private float sampleRadius = 1.5f;

        private void Update()
        {
            if (cameraManager != null && cameraManager.InputLocked) return;
            if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;
            if (menu == null || menu.IsOpen || cameraManager == null || mover == null || marker == null) return;
            if (cameraManager.ActiveCamera == null) return;
            Camera camera = cameraManager.ActiveCamera.GetComponent<Camera>();
            if (camera == null) return;
            Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, raycastMask, QueryTriggerInteraction.Ignore)) return;
            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, sampleRadius, NavMesh.AllAreas)) return;
            mover.MoveTo(navHit.position);
            marker.Show(navHit.position);
        }
    }
}

