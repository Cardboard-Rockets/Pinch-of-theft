using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PinchOfTheft.CCTV
{
    public sealed class CameraManager : MonoBehaviour
    {
        [SerializeField] private CCTVCamera[] cameras;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private SphereInteractor interactor;
        [SerializeField] private BoardController board;
        [SerializeField] private DoorController door;
        private BoardController[] sceneBoards;
        private DoorController[] sceneDoors;
        private bool IsBoardAimed()
        {
            if (sceneBoards != null) foreach (var candidate in sceneBoards)
                if (candidate != null && candidate.isActiveAndEnabled && candidate.IsAimed) return true;
            return false;
        }
        private bool IsDoorAimed()
        {
            if (sceneDoors != null) foreach (var candidate in sceneDoors)
                if (candidate != null && candidate.isActiveAndEnabled && candidate.IsAimed) return true;
            return false;
        }
        public bool InputLocked { get => GameContext.Instance != null && GameContext.Instance.InputLock.IsLocked; set { if (GameContext.Instance == null) return; if (value) GameContext.Instance.InputLock.Acquire(this); else GameContext.Instance.InputLock.Release(this); } }
        public event System.Action<CCTVCamera> ActiveCameraChanged;
        private void Awake()
        {
            if (cameras == null || cameras.Length == 0) {
                cameras = GetComponentsInChildren<CCTVCamera>(true);
                if (cameras.Length == 0) cameras = Object.FindObjectsByType<CCTVCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                System.Array.Sort(cameras, (a,b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Slot.CompareTo(b.Slot));
            }
        }
        private void OnEnable() { if (cameras != null) foreach (var camera in cameras) if (camera != null) camera.AccessibilityChanged += AvailabilityChanged; }
        private void OnDisable() { if (cameras != null) foreach (var camera in cameras) if (camera != null) camera.AccessibilityChanged -= AvailabilityChanged; }
        private void AvailabilityChanged() { if (menu != null) EnsureAvailableCamera(); }
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.15f;
        public IReadOnlyList<CCTVCamera> Cameras => cameras;
        public CCTVCamera ActiveCamera { get; private set; }

        public void Configure(CCTVCamera[] views, CameraMenuUI cameraMenu)
        {
            cameras = views;
            menu = cameraMenu;
        }

        public void UnlockGroup(int group)
        {
            if (cameras == null) return;
            foreach (CCTVCamera camera in cameras)
            {
                if (camera == null || camera.Group != group) continue;
                camera.SetAccessible(true);
                camera.SetVisibleInMenu(true);
            }
        }

        private void Start()
        {
            if (!GameContext.Require(this)) return;
            if (menu == null) menu = GameContext.Instance.Menu;
            if (interactor == null) interactor = GetComponentInChildren<SphereInteractor>(true);
            if (board == null) board = Object.FindAnyObjectByType<BoardController>();
            sceneBoards = Object.FindObjectsByType<BoardController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if (door == null) door = Object.FindAnyObjectByType<DoorController>();
            sceneDoors = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            menu.Initialize(this);
            menu.SetCameraLabel("NO SIGNAL");

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
            if (menu == null) return;
            if (InputLocked) { if (menu.IsOpen && !GameContext.Instance.InputLock.HasOtherOwner(menu) && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) menu.SetOpen(false); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                menu.SetOpen(!menu.IsOpen);
                return;
            }
            if (menu.IsOpen || ActiveCamera == null) return;

            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) SwitchCamera(-1);
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame && (interactor == null || !interactor.HasTarget) && !IsBoardAimed() && !IsDoorAimed()) SwitchCamera(1);
            if (Mouse.current != null)
                ActiveCamera.Rotate(Mouse.current.delta.ReadValue() * mouseSensitivity);
        }

        private void SwitchCamera(int direction)
        {
            if (menu.IsOpen || ActiveCamera == null) return;
            CCTVCamera next = null;
            int nearest = int.MaxValue;
            foreach (var candidate in cameras)
            {
                if (candidate == null || !candidate.isAccessible || !candidate.isActiveAndEnabled || candidate.Group != ActiveCamera.Group) continue;
                int distance = (candidate.Slot - ActiveCamera.Slot) * direction;
                if (distance <= 0 || distance >= nearest) continue;
                next = candidate;
                nearest = distance;
            }
            if (next != null) SelectCamera(next);
        }

        private CCTVCamera FirstAvailable()
        {
            for (int group = 0; group <= 9; group++)
                for (int slot = 1; slot <= 9; slot++)
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
                
                int nearestDistance = int.MaxValue;
                int lastSlot = 4;
                foreach (var camera in cameras)
                    if (camera != null && camera.Group == ActiveCamera.Group) lastSlot = Mathf.Max(lastSlot, camera.Slot);
                for (int slot = 1; slot <= lastSlot; slot++)
                {
                    CCTVCamera candidate = FindCamera(ActiveCamera.Group, slot);
                    int distance = Mathf.Abs(slot - ActiveCamera.Slot);
                    distance = Mathf.Min(distance, lastSlot - distance);
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
            ActiveCameraChanged?.Invoke(null);
            menu.SetCameraLabel("NO SIGNAL");
        }

        public void SelectCamera(CCTVCamera selected)
        {
            if (selected == null || !selected.isAccessible || !selected.isActiveAndEnabled || cameras == null ||
                System.Array.IndexOf(cameras, selected) < 0) return;
            foreach (CCTVCamera camera in cameras)
                if (camera != null) camera.SetActive(camera == selected);
            ActiveCamera = selected;
            ActiveCameraChanged?.Invoke(selected);
            menu.SetCameraLabel("CAM " + selected.CameraName);
            menu.SetOpen(false);
        }
    }
}








