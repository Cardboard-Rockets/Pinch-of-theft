using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PinchOfTheft.CCTV
{
    public sealed class CameraManager : MonoBehaviour
    {
        [SerializeField] private CCTVCamera[] cameras;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.15f;
        public IReadOnlyList<CCTVCamera> Cameras => cameras;
        public CCTVCamera ActiveCamera { get; private set; }

        public void Configure(CCTVCamera[] views, CameraMenuUI cameraMenu)
        {
            cameras = views;
            menu = cameraMenu;
        }

        private void Start()
        {
            menu.Initialize(this);
            menu.SetCameraLabel("NO SIGNAL");
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (cameras == null) return;
            foreach (CCTVCamera camera in cameras)
                if (camera != null) camera.SetActive(false);
            SelectCamera(FindCamera(0, 1) ?? FirstAvailable());
        }

        private CCTVCamera FindCamera(int group, int slot)
        {
            if (cameras == null) return null;
            foreach (CCTVCamera camera in cameras)
                if (camera != null && camera.isAccessible && camera.isActiveAndEnabled &&
                    camera.Group == group && camera.Slot == slot) return camera;
            return null;
        }

        private void Update()
        {
            EnsureAvailableCamera();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                menu.SetOpen(!menu.IsOpen);
                return;
            }
            if (menu.IsOpen || ActiveCamera == null) return;

            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) SwitchCamera(-1);
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame) SwitchCamera(1);
            if (Mouse.current != null)
                ActiveCamera.Rotate(Mouse.current.delta.ReadValue() * mouseSensitivity);
        }

        private void SwitchCamera(int direction)
        {
            if (menu.IsOpen || ActiveCamera == null) return;
            int current = ActiveCamera.Slot - 1;
            for (int step = 1; step < 4; step++)
            {
                int index = current + direction * step;
                if (index < 0 || index >= 4) return;
                CCTVCamera candidate = FindCamera(ActiveCamera.Group, index + 1);
                if (candidate == null || candidate == ActiveCamera) continue;
                SelectCamera(candidate);
                return;
            }
        }

        private CCTVCamera FirstAvailable()
        {
            for (int group = 0; group < 3; group++)
                for (int slot = 1; slot <= 4; slot++)
                {
                    CCTVCamera camera = FindCamera(group, slot);
                    if (camera != null) return camera;
                }
            return null;
        }

        private void EnsureAvailableCamera()
        {
            if (ActiveCamera != null && ActiveCamera.isAccessible && ActiveCamera.isActiveAndEnabled) return;
            CCTVCamera replacement = null;
            if (ActiveCamera != null)
            {
                // Circular slot distance; ties prefer the lower slot number.
                int nearestDistance = int.MaxValue;
                for (int slot = 1; slot <= 4; slot++)
                {
                    CCTVCamera candidate = FindCamera(ActiveCamera.Group, slot);
                    int distance = Mathf.Abs(slot - ActiveCamera.Slot);
                    distance = Mathf.Min(distance, 4 - distance);
                    if (candidate == null || distance >= nearestDistance) continue;
                    replacement = candidate;
                    nearestDistance = distance;
                }
            }
            replacement = replacement != null ? replacement : FirstAvailable();
            if (replacement != null)
            {
                bool wasOpen = menu.IsOpen;
                SelectCamera(replacement);
                if (wasOpen) menu.SetOpen(true);
                return;
            }
            if (cameras != null)
                foreach (CCTVCamera camera in cameras)
                    if (camera != null) camera.SetActive(false);
            ActiveCamera = null;
            menu.SetCameraLabel("NO SIGNAL");
        }

        public void SelectCamera(CCTVCamera selected)
        {
            if (selected == null || !selected.isAccessible || !selected.isActiveAndEnabled || cameras == null ||
                System.Array.IndexOf(cameras, selected) < 0) return;
            foreach (CCTVCamera camera in cameras)
                if (camera != null) camera.SetActive(camera == selected);
            ActiveCamera = selected;
            menu.SetCameraLabel("CAM " + selected.CameraName);
            menu.SetOpen(false);
        }
    }
}




