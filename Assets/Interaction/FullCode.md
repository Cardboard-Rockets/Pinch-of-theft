# Полный код системы взаимодействия

## Assets/Interaction/Scripts/InteractableSphere.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [SerializeField] private int unlockGroup;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        public bool IsAvailable { get; private set; } = true;

        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress);
            prompt.SetHighlighted(highlighted);
        }

        public void Complete()
        {
            if (!IsAvailable || cameraManager == null) return;
            cameraManager.UnlockGroup(unlockGroup);
            IsAvailable = false;
            if (prompt != null) prompt.Hide();
        }
    }
}

```

## Assets/Interaction/Scripts/SphereInteractor.cs

```csharp
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

```

## Assets/Interaction/Scripts/KeyPrompt.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class KeyPrompt : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fill;
        private void Awake() => SetHighlighted(false);
        public void SetHighlighted(bool value)
        {
            if (canvasGroup != null) canvasGroup.alpha = value ? 1f : 0f;
        }
        public void SetProgress(float value)
        {
            if (fill != null) fill.fillAmount = Mathf.Clamp01(value);
        }
        public void Hide() => gameObject.SetActive(false);
        private void LateUpdate()
        {
            if (cameraManager != null && cameraManager.ActiveCamera != null)
                transform.rotation = cameraManager.ActiveCamera.transform.rotation;
        }
    }
}

```

## Assets/CCTV/Scripts/CameraManager.cs

```csharp
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
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame && (interactor == null || !interactor.HasTarget)) SwitchCamera(1);
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






```

## Assets/CCTV/Scripts/CCTVCamera.cs

```csharp
using UnityEngine;
using UnityEngine.Serialization;

namespace PinchOfTheft.CCTV
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class CCTVCamera : MonoBehaviour
    {
        [SerializeField, Range(0, 2)] private int group;
        [SerializeField, Range(1, 4)] private int slot = 1;
        [SerializeField] private bool isVisibleInMenu = true;
        [FormerlySerializedAs("maxRotationAngle")]
        [SerializeField, Range(0f, 180f)] private float maxYawAngle = 40f;
        [SerializeField, Range(0f, 89f)] private float maxPitchAngle = 30f;
        public bool isAccessible = true;
        public void SetAccessible(bool value) => isAccessible = value;
        private Quaternion initialRotation;
        private float yaw;
        private float pitch;
        private Camera view;
        private AudioListener listener;

        public int Group => group;
        public int Slot => slot;
        public string CameraName => $"{group}{slot}";
        public bool IsVisibleInMenu => isVisibleInMenu;
        public void SetVisibleInMenu(bool value) => isVisibleInMenu = value;

        private void Awake()
        {
            initialRotation = transform.localRotation;
            view = GetComponent<Camera>();
            listener = GetComponent<AudioListener>();
        }

        public void SetActive(bool active)
        {
            if (view == null) view = GetComponent<Camera>();
            if (listener == null) listener = GetComponent<AudioListener>();
            view.enabled = active;
            listener.enabled = active;
        }

        public void Rotate(Vector2 delta)
        {
            yaw = Mathf.Clamp(yaw + delta.x, -maxYawAngle, maxYawAngle);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitchAngle, maxPitchAngle);
            transform.localRotation = initialRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}



```


