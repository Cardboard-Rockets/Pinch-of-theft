# Полный код головоломки и изменённых скриптов

## Assets/Interaction/Scripts/CodePuzzle.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class CodePuzzle : MonoBehaviour
    {
        [SerializeField] private InteractableSphere panelInteractable;
        [SerializeField] private KeypadUI keypad;
        [SerializeField] private Text tvText;
        [SerializeField, Range(1, 8)] private int codeLength = 4;
        [SerializeField] private bool useFixedCode;
        [SerializeField] private string fixedCode = "1234";
        [SerializeField] private string tvIdleText = "";
        [SerializeField] private string tvSolvedText = "OK";
        [SerializeField] private string successMessage = "Код принят: доступ разрешён";
        [SerializeField] private bool logWrongAttempts;
        [SerializeField] private string wrongMessage = "Неверный код";
        [SerializeField] private UnityEvent onSolved = new();
        [SerializeField] private UnityEvent onWrong = new();
        private string code;
        private bool solved;
        public int CodeLength => Mathf.Clamp(codeLength, 1, 8);
        public bool CanOpen => code != null && !solved;
        private void Start()
        {
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            if (tvText != null) tvText.text = tvIdleText;
        }
        public void OnShieldActivated()
        {
            if (code != null) return;
            bool valid = fixedCode != null && fixedCode.Length == CodeLength;
            if (valid) foreach (char c in fixedCode) if (c < '0' || c > '9') valid = false;
            code = useFixedCode && valid ? fixedCode : "";
            if (code.Length == 0) for (int i = 0; i < CodeLength; i++) code += Random.Range(0, 10).ToString();
            if (tvText != null) tvText.text = code;
            if (panelInteractable != null) panelInteractable.SetAvailable(true);
        }
        public bool CheckCode(string input)
        {
            if (!CanOpen) return false;
            if (input != code)
            {
                onWrong.Invoke();
                if (logWrongAttempts) Debug.Log(wrongMessage);
                if (keypad != null) keypad.ShowError();
                return false;
            }
            solved = true;
            Debug.Log(successMessage);
            onSolved.Invoke();
            if (tvText != null) tvText.text = tvSolvedText;
            if (keypad != null) keypad.Close();
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            return true;
        }
    }
}

```

## Assets/Interaction/Scripts/KeypadUI.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class KeypadUI : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private CodePuzzle puzzle;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text display;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private GameObject cameraLabel;
        [SerializeField] private bool maskInput;
        [SerializeField] private bool allowKeyboardInput = true;
        [SerializeField, Min(0f)] private float errorShowTime = 1f;
        private string input = "";
        private float errorUntil;
        public bool IsOpen { get; private set; }
        public void Open()
        {
            if (cameraManager == null || menu == null || menu.IsOpen || puzzle == null || !puzzle.CanOpen || panel == null) return;
            IsOpen = true; cameraManager.InputLocked = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (crosshair != null) crosshair.SetActive(false);
            if (cameraLabel != null) cameraLabel.SetActive(false);
            input = ""; errorUntil = 0; panel.SetActive(true); Refresh();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (panel != null) panel.SetActive(false);
            if (cameraManager != null) cameraManager.InputLocked = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            if (crosshair != null) crosshair.SetActive(true);
            if (cameraLabel != null) cameraLabel.SetActive(true);
        }
        public void AddDigit(int digit)
        {
            if (!IsOpen || puzzle == null || digit < 0 || digit > 9 || Time.unscaledTime < errorUntil || input.Length >= puzzle.CodeLength) return;
            input += digit.ToString(); Refresh();
        }
        public void Erase()
        {
            if (!IsOpen || input.Length == 0) return;
            input = input.Substring(0, input.Length - 1); Refresh();
        }
        public void Submit()
        {
            if (IsOpen && puzzle != null && input.Length == puzzle.CodeLength) puzzle.CheckCode(input);
        }
        public void ShowError() { input = ""; errorUntil = Time.unscaledTime + errorShowTime; Refresh(); }
        private void Refresh()
        {
            if (display == null || puzzle == null) return;
            display.text = Time.unscaledTime < errorUntil ? "ERROR" :
                (maskInput ? new string('*', input.Length) : input).PadRight(puzzle.CodeLength, '_');
        }
        private void Update()
        {
            if (!IsOpen) return;
            if (panel == null || !panel.activeInHierarchy) { Close(); return; }
            Refresh();
            var keyboard = Keyboard.current;
            if (!allowKeyboardInput || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            for (int digit = 0; digit <= 9; digit++)
            {
                Key main = digit == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + digit - 1);
                Key num = digit == 0 ? Key.Numpad0 : (Key)((int)Key.Numpad1 + digit - 1);
                if (keyboard[main].wasPressedThisFrame || keyboard[num].wasPressedThisFrame) AddDigit(digit);
            }
            if (keyboard.backspaceKey.wasPressedThisFrame) Erase();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Submit();
        }
        private void OnDisable() { if (IsOpen) Close(); }
    }
}

```

## Assets/Interaction/Scripts/BoardController.cs

```csharp
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




```

## Assets/Interaction/Scripts/InteractableSphere.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [SerializeField] private int unlockGroup = -1;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField, Min(0f)] private float holdDuration;
        [SerializeField] private bool singleUse = true;
        [SerializeField] private bool startAvailable = true;
        public bool IsAvailable { get; private set; } = true;
        public float HoldDuration => holdDuration;
        private void Awake() => SetAvailable(startAvailable);
        public void SetAvailable(bool value)
        {
            IsAvailable = value;
            if (prompt == null) return;
            if (!value) prompt.Hide();
            else { prompt.gameObject.SetActive(true); prompt.SetProgress(0); prompt.SetHighlighted(false); }
        }
        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress); prompt.SetHighlighted(highlighted);
        }
        public void Complete()
        {
            if (!IsAvailable) return;
            if (unlockGroup >= 0 && cameraManager != null) cameraManager.UnlockGroup(unlockGroup);
            if (singleUse) SetAvailable(false);
            onCompleted.Invoke();
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
            if (cameraManager != null && cameraManager.InputLocked) { ResetTarget(); return; }
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
            else progress += Time.deltaTime / Mathf.Max(0.01f, target.HoldDuration > 0f ? target.HoldDuration : holdDuration);
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
        [SerializeField] private BoardController board;
        public bool InputLocked { get; set; }
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
            if (InputLocked) return;
            EnsureAvailableCamera();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                menu.SetOpen(!menu.IsOpen);
                return;
            }
            if (menu.IsOpen || ActiveCamera == null) return;

            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) SwitchCamera(-1);
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame && (interactor == null || !interactor.HasTarget) && (board == null || !board.IsAimed)) SwitchCamera(1);
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

## Assets/CCTV/Scripts/CameraMenuUI.cs

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.CCTV
{
    public sealed class CameraMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform[] groupContainers;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Color activeCameraColor = new Color(0.15f, 0.7f, 0.5f);
        [SerializeField] private GameObject crosshair;
        [SerializeField] private Text cameraLabel;
        private readonly System.Collections.Generic.Dictionary<Button, CCTVCamera> buttons = new();
        private CameraManager manager;
        public bool IsOpen => panel.activeSelf;

        public void Configure(GameObject menuPanel, Transform[] containers, Button template, GameObject aim = null, Text label = null)
        {
            panel = menuPanel;
            crosshair = aim;
            cameraLabel = label;
            groupContainers = containers;
            buttonTemplate = template;
        }

        public void SetCameraLabel(string text)
        {
            if (cameraLabel != null) cameraLabel.text = text;
        }

        public void Initialize(CameraManager cameraManager)
        {
            manager = cameraManager;
            panel.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (crosshair != null) crosshair.SetActive(!open);
            if (cameraLabel != null) cameraLabel.gameObject.SetActive(!open);
            if (open == IsOpen) return;
            if (open) RebuildButtons();
            panel.SetActive(open);
        }

        private void LateUpdate()
        {
            if (manager != null && manager.InputLocked) return;
            if (crosshair != null)
                crosshair.SetActive(!IsOpen && Cursor.lockState == CursorLockMode.Locked);
            if (!IsOpen) return;
            foreach (var entry in buttons)
            {
                bool available = entry.Value != null && entry.Value.isAccessible && entry.Value.isActiveAndEnabled;
                entry.Key.interactable = available;
                entry.Key.image.color = !available ? Color.gray :
                    entry.Value == manager.ActiveCamera ? activeCameraColor : buttonTemplate.image.color;
            }
        }

        private void RebuildButtons()
        {
            buttons.Clear();
            foreach (Transform container in groupContainers)
                foreach (Transform child in container)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            for (int group = 0; group < groupContainers.Length; group++)
                for (int slot = 1; slot <= 4; slot++)
                    foreach (CCTVCamera camera in manager.Cameras)
                    {
                        if (camera == null || !camera.IsVisibleInMenu || !camera.isActiveAndEnabled ||
                            camera.Group != group || camera.Slot != slot) continue;
                        CCTVCamera selected = camera;
                        Button button = Instantiate(buttonTemplate, groupContainers[group]);
                        buttons.Add(button, camera);
                        button.interactable = camera.isAccessible;
                        button.name = "Select_" + camera.CameraName;
                        button.GetComponentInChildren<Text>().text = camera.CameraName;
                        if (camera == manager.ActiveCamera)
                            button.image.color = activeCameraColor;
                        button.onClick.AddListener(() => manager.SelectCamera(selected));
                        button.gameObject.SetActive(true);
                    }
        }

        private void OnDisable()
        {
            if (panel != null && IsOpen) SetOpen(false);
        }
    }
}




```

## Assets/Movement/Scripts/PlayerMover.cs

```csharp
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


```

## Assets/Movement/Scripts/ClickToMoveInput.cs

```csharp
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


```

## Assets/Interaction/Editor/PuzzleSceneSetup.cs

```csharp
using System.Linq;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction.Editor
{
    public static class PuzzleSceneSetup
    {
        private static void Set(Object target, string field, Object value)
        { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Number(Object target, string field, float value)
        { var s = new SerializedObject(target); var p = s.FindProperty(field); if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0; else p.floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static Text Label(string name, Transform parent, string text, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = position;
            var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 32; label.text = text; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private static GameObject WorldCanvas(string name, Transform parent, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.localScale = Vector3.one * 0.005f;
            go.GetComponent<RectTransform>().sizeDelta = size; go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; return go;
        }
        private static Image Image(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size; var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        private static InteractableSphere Interaction(GameObject go, CameraManager manager, int layer)
        {
            go.layer = layer; if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
            var interaction = go.AddComponent<InteractableSphere>(); Set(interaction, "cameraManager", manager); Number(interaction, "unlockGroup", -1);
            var bounds = go.GetComponent<Renderer>() != null ? go.GetComponent<Renderer>().bounds : new Bounds(go.transform.position, Vector3.one);
            var canvas = WorldCanvas("KeyPrompt", go.transform, bounds.center + Vector3.up * (bounds.extents.y + 0.4f), new Vector2(80,80));
            var alpha = canvas.AddComponent<CanvasGroup>(); alpha.blocksRaycasts = false; alpha.alpha = 0;
            Image("Background", canvas.transform, new Vector2(80,80)).color = Color.black;
            var fill = Image("Fill", canvas.transform, new Vector2(80,80)); fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical; fill.fillOrigin = 0; fill.fillAmount = 0;
            Label("E", canvas.transform, "E", new Vector2(80,80), Vector2.zero).color = Color.cyan;
            var prompt = canvas.AddComponent<KeyPrompt>(); Set(prompt,"cameraManager",manager); Set(prompt,"canvasGroup",alpha); Set(prompt,"fill",fill); Set(interaction,"prompt",prompt); return interaction;
        }
        private static void Button(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            var image = Image(title,parent,new Vector2(160,70)); image.rectTransform.anchoredPosition = pos; image.color = new Color(0.15f,0.22f,0.3f); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Label("Text",image.transform,title,new Vector2(160,70),Vector2.zero).fontSize = 23;
            // Persistent callbacks are configured separately for scene buttons.
        }
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            string[][] names = {new[]{"board","доска"},new[]{"panel","панель"},new[]{"tv","тв"},new[]{"shield","щит"}};
            var objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t=>t.gameObject).ToArray();
            var found = names.Select(n=>objects.Where(o=>n.Contains(o.name.ToLowerInvariant())).ToArray()).ToArray();
            if (found.Any(a=>a.Length!=1)) throw new System.Exception("Ambiguous objects: " + string.Join(";",found.Select(a=>string.Join(",",a.Select(o=>o.name)))));
            var boardObject=found[0][0]; var panelObject=found[1][0]; var tv=found[2][0]; var shield=found[3][0];
            var manager=Object.FindAnyObjectByType<CameraManager>(); var menu=Object.FindAnyObjectByType<CameraMenuUI>(); var mover=Object.FindAnyObjectByType<PlayerMover>(); var marker=Object.FindAnyObjectByType<ClickMarker>(FindObjectsInactive.Include);
            int layer=LayerMask.NameToLayer("Interactable"); int mask=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));
            var interactor=Object.FindAnyObjectByType<SphereInteractor>(); Number(interactor,"interactMask",mask);
            var puzzle=new GameObject("CodePuzzle").AddComponent<CodePuzzle>();
            var keypad=puzzle.gameObject.AddComponent<KeypadUI>(); Set(keypad,"cameraManager",manager); Set(keypad,"menu",menu); Set(keypad,"puzzle",puzzle); Set(puzzle,"keypad",keypad);
            var canvas=Object.FindObjectsByType<Canvas>().First(c=>c.renderMode==RenderMode.ScreenSpaceOverlay);
            var ui=new GameObject("KeypadPanel",typeof(RectTransform),typeof(Image)); ui.transform.SetParent(canvas.transform,false); ui.GetComponent<RectTransform>().sizeDelta=new Vector2(600,620); ui.GetComponent<Image>().color=new Color(0.03f,0.04f,0.06f,0.98f);
            Set(keypad,"panel",ui); Set(keypad,"display",Label("Display",ui.transform,"____",new Vector2(500,80),new Vector2(0,240)));
            var menuSettings=new SerializedObject(menu); Set(keypad,"crosshair",menuSettings.FindProperty("crosshair").objectReferenceValue); var hud=menuSettings.FindProperty("cameraLabel").objectReferenceValue as Text; if(hud!=null)Set(keypad,"cameraLabel",hud.gameObject);
            int[] digits={1,2,3,4,5,6,7,8,9,-1,0,-2};
            for(int i=0;i<12;i++)
            {
                int digit=digits[i]; string title=digit==-1?"Стереть":digit==-2?"Подтвердить":digit.ToString();
                Button(ui.transform,title,new Vector2((i%3-1)*180,130-(i/3)*100),()=>{});
                var button=ui.transform.Find(title).GetComponent<UnityEngine.UI.Button>();
                if(digit>=0)UnityEventTools.AddIntPersistentListener(button.onClick,keypad.AddDigit,digit);
                else if(digit==-1)UnityEventTools.AddPersistentListener(button.onClick,keypad.Erase);
                else UnityEventTools.AddPersistentListener(button.onClick,keypad.Submit);
            }
            Button(ui.transform,"X",new Vector2(210,300),()=>{}); UnityEventTools.AddPersistentListener(ui.transform.Find("X").GetComponent<UnityEngine.UI.Button>().onClick,keypad.Close); ui.SetActive(false);
            var panel=Interaction(panelObject,manager,layer); Number(panel,"singleUse",0); Number(panel,"startAvailable",0); Number(panel,"holdDuration",0.5f); Set(puzzle,"panelInteractable",panel);
            var shieldInteraction=Interaction(shield,manager,layer); Number(shieldInteraction,"holdDuration",2);
            var eventField=typeof(InteractableSphere).GetField("onCompleted",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent)eventField.GetValue(panel),keypad.Open);
            UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent)eventField.GetValue(shieldInteraction),puzzle.OnShieldActivated);
            var tvBounds=tv.GetComponent<Renderer>()!=null?tv.GetComponent<Renderer>().bounds:new Bounds(tv.transform.position,Vector3.one);
            var screen=WorldCanvas("TV Screen",tv.transform,tvBounds.center-tv.transform.forward*(tvBounds.extents.z+0.02f),new Vector2(200,120)); screen.transform.rotation=tv.transform.rotation;
            Image("Background",screen.transform,new Vector2(200,120)).color=new Color(0.01f,0.02f,0.02f); var tvText=Label("Code",screen.transform,"",new Vector2(200,120),Vector2.zero); tvText.fontSize=48; tvText.color=Color.green; Set(puzzle,"tvText",tvText);
            var board=new GameObject("BoardController").AddComponent<BoardController>(); Set(board,"cameraManager",manager); Set(board,"menu",menu); Set(board,"mover",mover); Set(board,"marker",marker); Set(board,"standingBoard",boardObject); Set(manager,"board",board); Number(board,"mask",mask);
            if(boardObject.GetComponent<Collider>()==null)boardObject.AddComponent<BoxCollider>();
            var fallen=Object.Instantiate(boardObject,boardObject.transform.parent); fallen.name="FallenBoard"; var bounds=boardObject.GetComponent<Renderer>().bounds; fallen.transform.Rotate(Vector3.right,90,Space.World); fallen.transform.position=bounds.center+boardObject.transform.forward*(bounds.extents.y+0.2f); var fallenBounds=fallen.GetComponent<Renderer>().bounds; fallen.transform.position+=Vector3.up*(0.06f-fallenBounds.min.y); var obstacle=fallen.AddComponent<NavMeshObstacle>(); obstacle.carving=true; obstacle.shape=NavMeshObstacleShape.Box; obstacle.size=fallen.GetComponent<MeshFilter>().sharedMesh.bounds.size; fallen.SetActive(false); Set(board,"fallenBoard",fallen);
            var point=new GameObject("ApproachPoint").transform; point.SetParent(boardObject.transform); var wanted=bounds.center-boardObject.transform.forward*(bounds.extents.z+1.2f); if(NavMesh.SamplePosition(wanted,out var hit,3,NavMesh.AllAreas))wanted=hit.position; point.position=wanted; Set(board,"approachPoint",point);
            var popup=WorldCanvas("BoardPopup",board.transform,bounds.center+Vector3.up*(bounds.extents.y+0.4f),new Vector2(240,70)); Image("Background",popup.transform,new Vector2(240,70)).color=Color.black; Label("Action",popup.transform,"E  Уронить",new Vector2(240,70),Vector2.zero); popup.SetActive(false); Set(board,"popup",popup);
            EditorUtility.SetDirty(panel); EditorUtility.SetDirty(shieldInteraction); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Debug.Log("PUZZLE_SETUP_COMPLETE");
        }
    }
}

```

## Assets/Interaction/Editor/PuzzlePlayCheck.cs

```csharp
using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.CCTV;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Interaction.Editor
{
    [InitializeOnLoad]
    public static class PuzzlePlayCheck
    {
        static PuzzlePlayCheck() { EditorApplication.playModeStateChanged += s => { if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PuzzleCheck",false)) EditorApplication.delayCall+=Check; }; }
        public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity"); SessionState.SetBool("PuzzleCheck",true); EditorApplication.EnterPlaymode(); }
        private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        private static void Check()
        {
            try
            {
                var puzzle=UnityEngine.Object.FindAnyObjectByType<CodePuzzle>(); var keypad=UnityEngine.Object.FindAnyObjectByType<KeypadUI>(); var manager=UnityEngine.Object.FindAnyObjectByType<CameraManager>();
                var panel=(InteractableSphere)Field(puzzle,"panelInteractable"); var tv=(Text)Field(puzzle,"tvText");
                Require(!panel.IsAvailable && tv.text=="" && !puzzle.CanOpen);
                var shield=GameObject.Find("shield").GetComponent<InteractableSphere>(); shield.Complete();
                Require(panel.IsAvailable && !shield.IsAvailable && tv.text.Length==4); string code=tv.text;
                puzzle.OnShieldActivated(); Require(tv.text==code);
                panel.Complete(); Require(keypad.IsOpen && manager.InputLocked);
                string wrong=code=="0000"?"1111":"0000"; Require(!puzzle.CheckCode(wrong)); Require(((Text)Field(keypad,"display")).text=="ERROR");
                keypad.Close(); Require(!manager.InputLocked); keypad.Open();
                foreach(char c in code)keypad.AddDigit(c-'0'); keypad.Submit();
                Require(!keypad.IsOpen && !manager.InputLocked && !panel.IsAvailable && tv.text=="OK"); keypad.Open(); Require(!keypad.IsOpen);
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled)==1);
                var board=UnityEngine.Object.FindAnyObjectByType<BoardController>(); Require(!((GameObject)Field(board,"fallenBoard")).activeSelf);
                Debug.Log("PUZZLE_PLAY_PASSED"); SessionState.SetBool("PuzzleCheck",false); EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e); SessionState.SetBool("PuzzleCheck",false);EditorApplication.Exit(1);}
        }
        private static void Require(bool condition){if(!condition)throw new Exception("Puzzle check failed");}
    }
}

```

