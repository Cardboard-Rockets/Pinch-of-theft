using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-10)]
    public sealed class SphereInteractor : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private LayerMask interactMask;
        [SerializeField, Min(0f)] private float maxDistance = 15f;
        [SerializeField, Min(0.01f)] private float holdDuration = 2f;
        private InteractableSphere target;
        private CCTVCamera previousCamera;
        private float progress;
        private bool waitForRelease;
        public bool HasTarget => target != null && target.IsAvailable;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) { ResetTarget(); return; }
            if (!keyboard.eKey.isPressed) waitForRelease = false;
            CCTVCamera active = cameraManager != null ? cameraManager.ActiveCamera : null;
            bool cameraChanged = active != previousCamera;
            previousCamera = active;
            InteractableSphere next = null;
            if (menu != null && !menu.IsOpen && !keyboard.tabKey.wasPressedThisFrame && active != null)
            {
                Camera camera = active.GetComponent<Camera>();
                if (camera != null)
                {
                    Ray ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
                    if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactMask, QueryTriggerInteraction.Ignore))
                    {
                        next = hit.collider.GetComponentInParent<InteractableSphere>();
                        if (next != null && !next.IsAvailable) next = null;
                    }
                }
            }
            if (cameraChanged || next != target) ResetTarget();
            target = next;
            if (!HasTarget) return;
            if (cameraChanged || !keyboard.eKey.isPressed || waitForRelease) progress = 0f;
            else progress += Time.deltaTime / Mathf.Max(0.01f, holdDuration);
            target.SetVisual(progress, true);
            if (progress < 1f) return;
            target.Complete();
            waitForRelease = true;
            ResetTarget();
        }

        private void ResetTarget()
        {
            if (target != null) target.SetVisual(0f, false);
            target = null;
            progress = 0f;
        }
        private void OnDisable() => ResetTarget();
    }
}
